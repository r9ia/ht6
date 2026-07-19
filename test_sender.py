import socket
import json

PORT = 5005

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
payload = json.dumps({
    "type": "hit",
    "magnitude_g": 3.14,
    "reason": "test_packet"
}).encode("utf-8")

sock.sendto(payload, ("127.0.0.1", PORT))
print(f"sent test packet to 127.0.0.1:{PORT}")