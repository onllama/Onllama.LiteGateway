# Onllama.LiteGateway 
The easiest way to add Apikey authentication to Ollama is to prevent it from being abused by exposing it directly to the public network. 

防止 Ollama 直接暴露在公网受到滥用，为 Ollama 添加 Apikey 鉴权的最简单方式。 

```
wget https://github.com/onllama/Onllama.LiteGateway/releases/latest/download/Onllama.LiteGateway.linux-x64 -O /usr/bin/Onllama.LiteGateway
wget https://raw.githubusercontent.com/onllama/Onllama.LiteGateway/refs/heads/main/onllama-litegateway@.service -O /etc/systemd/system/onllama-litegateway@.service
chmod +x /usr/bin/Onllama.LiteGateway 
systemctl enable onllama-litegateway@sk-just-for-example --now

# Please replace 'sk-just-for-example' with the apikey you want to set. / 请替换 'sk-just-for-example' 为你想要设置的 apikey。
# curl http://127.0.0.1:22434
```

## TODO
- [ ] HTTPS
- [ ] Log
- [ ] Rate Limit
- [ ] Public URL
- [ ] Trim <think/>
- [ ] Apikey from DB
