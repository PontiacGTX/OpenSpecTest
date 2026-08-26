Requirements:

SDK ASP.NET 8

install Ollama 
irm https://ollama.com/install.ps1 | iex

llama 3.2 3b model
ollama run llama3.2:3b

sql server database
docker

run run-all.ps1 over powershell

Docker usa Ollama instalado en Windows, no una imagen de Ollama:

```powershell
$env:OLLAMA_HOST="0.0.0.0:11434"
ollama serve
```

En otra terminal:

```powershell
docker compose -f OpenSpecTest/docker-compose.yml up --build
```

La API quedara disponible en `http://localhost:5000` y se conectara a
`http://host.docker.internal:11434/v1` usando `llama3.2:3b`.
