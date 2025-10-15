# -*- coding: utf-8 -*-
"""
Created on Tue Oct 14 10:36:17 2025

@author: Abin Jacob
         Co-founder of CalypsoBCI
         abin@calypsobci.com
    

© 2025 CalypsoBCI. All rights reserved.
This code and its accompanying documentation, including all descriptions, comments, and 
explanatory text, are the intellectual property of CalypsoBCI. Unauthorized reproduction, 
distribution, or use of any portion of this material, in any form or by any means, without 
explicit written permission is strictly prohibited.

"""

import socket
import threading
from datetime import datetime
import struct
import time
import numpy as np


FLOAT_TYPE = 0x05


# coordinates of the target boxes (hard coded)
target_boxes = np.array([(-0.1, 0.05),  (0.0, 0.05),  (0.1, 0.05),
                         (-0.1, -0.05), (0.0, -0.05), (0.1, -0.05)])

FLK_RADIUS = 0.05

# find the fixated target
def findtarget(anchor):
    ax, ay = anchor
    distances = np.sqrt((target_boxes[:, 0] - ax)**2 + (target_boxes[:, 1] - ay)**2)
    idx = np.argmin(distances)
    return idx

# fixation classifier
class FixationClassifier:
    def __init__(self, sampling_rate=60, velocity_threshold=1,
                 min_fixation_duration=0.3, dwell_trigger=1.0, max_gap=0.05, radius_px=None):
        self.fs = sampling_rate
        self.vth = velocity_threshold
        self.min_fix_samples = max(1, int(min_fixation_duration * sampling_rate))
        self.dwell_trigger = dwell_trigger
        self.max_gap = max_gap
        self.radius_px = radius_px
        self.prev = None
        self.candidate_count = 0
        self.in_fixation = False
        self.fix_start_time = None
        self.last_below_time = None
        self.anchor_xy = None
        self.triggered = False  

    def process_point(self, timestamp, x, y):
        event = None
        anchor = self.anchor_xy if self.in_fixation else None

        if self.prev is None:
            self.prev = (timestamp, x, y)
            return 0, None, None

        dt = max(1e-6, timestamp - self.prev[0])
        dx = x - self.prev[1]
        dy = y - self.prev[2]
        vel = np.sqrt(dx*dx + dy*dy) / dt
        self.prev = (timestamp, x, y)

        below = vel < self.vth

        # enter maintain fixation
        if below:
            if not self.in_fixation:
                self.candidate_count += 1
                if self.candidate_count >= self.min_fix_samples:
                    self.in_fixation = True
                    self.fix_start_time = timestamp - (self.candidate_count-1)/self.fs
                    self.anchor_xy = (x, y)
                    self.triggered = False
                self.last_below_time = timestamp
            else:
                self.last_below_time = timestamp
                if self.radius_px is not None:
                    ax, ay = self.anchor_xy
                    self.anchor_xy = (0.95*ax + 0.05*x, 0.95*ay + 0.05*y)
        else:
            if self.in_fixation:
                if (timestamp - (self.last_below_time or timestamp)) > self.max_gap:
                    self.in_fixation = False
                    self.candidate_count = 0
                    self.anchor_xy = None
            else:
                self.candidate_count = 0
                self.anchor_xy = None

        # send fixation trigger
        if self.in_fixation and not self.triggered:
            if (timestamp - self.fix_start_time) >= self.dwell_trigger:
                event = 'trigger'
                self.triggered = True

        anchor = self.anchor_xy if self.in_fixation else None
        return (1 if self.in_fixation else 0), event, anchor

    def reset(self):
        self.in_fixation = False
        self.candidate_count = 0
        self.anchor_xy = None
        self.triggered = False
        self.fix_start_time = None
        self.prev = None

# sending TCP output
def sendOutput(conn, channel_name="ClassifierOut", event_code=1.0, x=0.0, y=0.0):
    payload = struct.pack('>fff', event_code, x, y)
    channel_bytes = channel_name.encode('ascii')
    channel_len = len(channel_bytes)
    payload_len = len(payload)
    timestamp = time.time()
    timestamp_bytes = struct.pack('>d', timestamp)
    type_byte = struct.pack('B', FLOAT_TYPE)
    channel_size_bytes = struct.pack('>I', channel_len)
    payload_size_bytes = struct.pack('>I', payload_len)
    package = timestamp_bytes + type_byte + channel_size_bytes + channel_bytes + payload_size_bytes + payload
    conn.sendall(package)

# initialise
HOST = '0.0.0.0'
PORT = 5000
server_running = threading.Event()
server_running.set()

trials_lock = threading.Lock()

# main logic
def clientHandler(conn, addr):
    global eyedata_packetloss, trials, anchor, cue_acc

    print(f"[NEW CONNECTION] {addr} connected.")

    # flags 
    collecting = False
    collecting_lock = threading.Lock()
    # fixation classifier
    fixClassifier = FixationClassifier(sampling_rate=60, velocity_threshold=0.5)
    cue_max_period = 5
    flk_max_period = 5
    current_anchor = None  
    flk_collecting = False
    FLK_REQUIRED_DURATION = 3.0  
    fixation_start_time = None
    eyedata_packetloss = 0
    trials  = 0
    cue_acc = 0
    flk_acc = 0

    conn.settimeout(1.0)

    while server_running.is_set():
        try:
            data = conn.recv(1024)
            if not data:
                break

            streamName = data[13:25].decode('utf-8', errors='ignore')

            # MarkerStream processing
            if 'MarkerStream' in streamName:
                markerText = data[29:34].decode('utf-8', errors='ignore')
                
                # for cue period
                if 'cue' in markerText:
                    with trials_lock:
                        trials += 1
                        trial_num = trials
                        targetidx = int(data[33:34].decode('utf-8', errors='ignore'))

                    print(f"[{datetime.now()}] [{streamName}] [Trial {trial_num}]: {markerText}")

                    # start collecting eye data and reset classifier
                    with collecting_lock:
                        collecting = True
                        fixClassifier.reset()
                        current_anchor = None

                    # Stop collection after cue duration
                    def end_cue():
                        nonlocal collecting
                        with collecting_lock:
                            collecting = False

                    threading.Timer(cue_max_period, end_cue).start()

                elif 'flk' in markerText:
                    print(f"[{datetime.now()}] [{streamName}] [Trial {trials}]: {markerText}")
                    # start flk collection
                    flk_collecting = True
                    fixation_start_time = None
                    
                    def end_flk():
                        nonlocal flk_collecting
                        if flk_collecting:
                            sendOutput(conn, event_code=2.0, x=1.0, y=0.0)
                            print(f"[Trial {trials}] Fixation not maintained, (acc= {(flk_acc/trials)*100})")
                            flk_collecting = False
                            fixation_start_time = None
                        flk_collecting = False
                
                    threading.Timer(flk_max_period, end_flk).start()

            
            # EyeStream processing
            elif 'EyeStream' in streamName:
                eyedata = data[36:].decode('utf-8', errors='ignore')
                try:
                    parts = eyedata.split(",")
                    if len(parts) == 3:
                        x, y, z = map(float, parts)
                        timestamp = time.time()
                        with collecting_lock:
                            if collecting:
                                label, event, anchor = fixClassifier.process_point(timestamp, x, y)
                                if event == 'trigger' and anchor is not None:
                                    current_anchor = anchor
                                    targetidx_pred = findtarget(current_anchor)
                                    if targetidx == (targetidx_pred+1):
                                        cue_acc += 1
                                        sendOutput(conn, event_code=1.0, x=0.0, y=0.0)
                                        print(f"[Trial {trials}] Fixated on target box, (acc= {(cue_acc/trials)*100})")
                                    else:
                                        sendOutput(conn, event_code=1.0, x=1.0, y=0.0)
                                        print(f"[Trial {trials}] Fixated on wrong box, (acc= {(cue_acc/trials)*100})")
                            elif flk_collecting and current_anchor is not None:
                                # check distance to cue anchor
                                dx = x - current_anchor[0]
                                dy = y - current_anchor[1]
                                dist = np.sqrt(dx*dx + dy*dy)
                                now = time.time()
                                
                                if dist <= FLK_RADIUS:
                                    if fixation_start_time is None:
                                        fixation_start_time = now
                                    elif now - fixation_start_time >= FLK_REQUIRED_DURATION:
                                        flk_acc +=1
                                        sendOutput(conn, event_code=2.0, x=0.0, y=0.0)
                                        print(f"[Trial {trials}] Fixation maintained on target, (acc= {(flk_acc/trials)*100})")
                                        flk_collecting = False
                                else:
                                    fixation_start_time = None
                                    
                                            
                    else:
                        eyedata_packetloss += 1
                except Exception:
                    eyedata_packetloss += 1

        except socket.timeout:
            continue
        except ConnectionResetError:
            print(f"[DISCONNECTED] {addr} forcibly closed the connection.")
            break
        except Exception as e:
            print(f"[ERROR] {e}")
            break

    conn.close()
    print(f"[CONNECTION CLOSED] {addr}")


def startServer():
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind((HOST, PORT))
    server.listen()
    print(f"[STARTING] TCP server on {HOST}:{PORT}")

    try:
        while server_running.is_set():
            server.settimeout(1.0)
            try:
                conn, addr = server.accept()
                thread = threading.Thread(target=clientHandler, args=(conn, addr), daemon=True)
                thread.start()
                print(f"[ACTIVE CONNECTIONS] {threading.active_count() - 1}")
            except socket.timeout:
                continue
    except KeyboardInterrupt:
        print("\n[SERVER SHUTDOWN] Interrupt received, shutting down...")
        server_running.clear()
    finally:
        server.close()
        print("[SERVER CLOSED]")


if __name__ == "__main__":
    startServer()




