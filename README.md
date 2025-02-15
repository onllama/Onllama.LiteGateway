
```
wget https://github.com/onllama/Onllama.LiteGateway/releases/latest/download/Onllama.LiteGateway.linux-x64 -O /usr/bin/Onllama.LiteGateway
wget https://raw.githubusercontent.com/onllama/Onllama.LiteGateway/refs/heads/main/onllama-litegateway@.service -O /etc/systemd/system/onllama-litegateway@.service
chmod +x /usr/bin/Onllama.LiteGateway 
systemctl enable onllama-litegateway@sk-just-for-example --now

# curl http://127.0.0.1:22434
```
