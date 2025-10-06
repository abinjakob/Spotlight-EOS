# -*- coding: utf-8 -*-
"""
Created on Sat Oct  4 09:41:34 2025

@author: messung
"""


import socket
import threading
from datetime import datetime
import struct
import time

FLOAT_TYPE = 0x05

def sendOutput(conn, channel_name="ClassifierOut", event_code=1.0, x=0.0, y=0.0):

    payload = struct.pack('>fff', event_code, x, y) 

    # channel name
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

# address and port
HOST = '0.0.0.0'
PORT = 5000

# thread-safe flag to control server state
server_running = threading.Event()
server_running.set()


eyedata_packetloss = 0
trials = 0

def clientHandler(conn, addr):
    global eyedata_packetloss, data, trials, trigger, markerText
    print(f"[NEW CONNECTION] {addr} connected.")

    # periodic checks for connection shutdown
    conn.settimeout(1.0) 
    while server_running.is_set():
        try:
            # receive data
            data = conn.recv(1024)
            if not data:
                break
            
            # fetch stream name 
            streamName = data[13:25].decode('utf-8', errors='ignore')
            
            
            # check if stream type markers
            if 'MarkerStream' in streamName:
                # fetch marker
                markerText = data[29:34].decode('utf-8', errors='ignore')
                
                if 'cue' in markerText:
                    trials +=1
                    print(f"[{datetime.now()}] [{streamName}] [Trial {trials}]: {markerText}")
                    time.sleep(4)
                    sendOutput(conn, event_code=1.0, x=0.0, y=0.0)
                    print('cue out sent')
                    
                elif 'flk' in markerText:
                    print(f"[{datetime.now()}] [{streamName}] [Trial {trials}]: {markerText}")
                    time.sleep(4)
                    sendOutput(conn, event_code=2.0, x=0.0, y=0.0)
                    print('flk out sent')
                
            
            # # check if stream type eyetracker
            # elif 'EyeStream' in streamName:
            #     # fetch eye data
            #     eyedata = data[36:].decode('utf-8', errors='ignore')
            #     try:
            #         parts = eyedata.split(",")
            #         if len(parts) == 3:
            #             x, y, z = map(float, parts)

            #         else:
            #             eyedata_packetloss +=1
            #     except Exception as e:
            #         eyedata_packetloss +=1
            
                    
                
        except socket.timeout:
            continue  # Check for shutdown flag again
        except ConnectionResetError:
            print(f"[DISCONNECTED] {addr} forcibly closed the connection.")
            break
        except Exception as e:
            print(f"[ERROR] {e}")
            break

    # close connection
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
