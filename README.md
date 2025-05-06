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

```
Onllama.LiteGateway - The simplest Ollama authentication way.
Copyright (c) 2025 Milkey Tan. Code released under the MIT License

Usage: Onllama.LiteGateway [options] <keys>

Arguments:
  keys                                     Setting up the API key

Options:
  -?|-he|--help                            Show help information.
  -l|--listen <IPEndPoint>                 Set server listening address and port
  -t|--target <Uri>                        Set target address and port
  --log                                    Enable logging
  -s|--https                               Set enable HTTPS (Self-signed by default, not recommended)
  -pem|--pemfile[:<FilePath>]              Set your pem certificate file path <./cert.pem>
  -key|--keyfile[:<FilePath>]              Set your pem certificate key file path <./cert.key>
  -h|--host[:<Hostname>]                   Set the allowed host names. (Allow all by default)
  --no-think-trim                          Disable ThinkTrim
  --no-token                               Disable API key verification
  --use-model-manage                       Enable model management via API
  --use-model-info-public-path             Allows get model information via API without APIKEY
  --use-cors-any                           Allow cross-origin requests
  --use-rate-limit                         Enable request rate limiting (with ipratelimiting.json)
  --num-ctx[:<NumCtx>]                     Set the number of contexts per request
  --use-input-security                     Enable input security
  --risk-model[:<RiskModel>]               Set the input security risk model
  --risk-model-prompt[:<RiskModelPrompt>]  Set the input security risk model prompt
  --risk-keywords <RiskKeywords>           Set the input security risk model keywords
```

## TODO
- [x] HTTPS
- [x] Host
- [x] Log
- [x] CORS
- [x] Rate Limit
- [x] Public URL
- [x] Trim `<think/>`
- [x] Override `num_ctx`
- [x] Input content security based on llamaguard/shieldgemma
- [ ] Auto context summary compress
- [ ] Apikey from DB
- [ ] ACME Auto HTTPS
