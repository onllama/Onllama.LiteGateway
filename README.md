# Onllama.LiteGateway 
Prevent Ollama from being abused by exposing it directly to the public network. The easiest way to add Apikey authentication to Ollama.

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
- [x] HTTPS
- [x] Host
- [x] Log
- [x] CORS
- [x] Rate Limit
- [x] Public URL
- [x] Trim `<think/>`
- [ ] Override `num_ctx`
- [ ] Input content security based on llama-guard
- [ ] Auto context summary compress
- [ ] Apikey from DB
- [ ] ACME Auto HTTPS
