#!/usr/bin/env python3
"""
udp_hit_listener.py

Minimal test listener — waits for UDP "hit" packets sent wirelessly from the
ESP32 (mpu6050_hit_esp32.ino) and prints them to the console. No game engine
hookup yet, just confirms the wireless signal chain works end to end:
MPU6050 -> ESP32 -> WiFi -> this script.

Run in VS Code:
    python udp_hit_listener.py

Make sure UNITY_PC_IP in the .ino matches this machine's actual LAN IP
(run `ipconfig` on Windows / `ifconfig` or `ip addr` on Mac/Linux to find it),
and that PORT below matches UDP_PORT in the .ino (default 5005).
"""

import socket
import json

PORT = 5005

def main():
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("0.0.0.0", PORT))
    print(f"listening for hits on UDP port {PORT}... (Ctrl+C to stop)")

    try:
        while True:
            data, addr = sock.recvfrom(1024)
            try:
                event = json.loads(data.decode("utf-8"))
                magnitude = event.get("magnitude_g", "?")
                reason = event.get("reason", "?")
                print(f"HIT DETECTED  from {addr[0]}  magnitude={magnitude}g  reason={reason}")
            except json.JSONDecodeError:
                print(f"received non-JSON packet from {addr[0]}: {data}")
    except KeyboardInterrupt:
        print("\nstopped")


if __name__ == "__main__":
    main()