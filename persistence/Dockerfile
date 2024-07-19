# Etapa de construção
FROM golang:1.22 AS builder

# Defina o diretório de trabalho dentro do contêiner
WORKDIR /app

# Copie os arquivos go.mod e go.sum
COPY go.mod go.sum ./

# Baixe as dependências
RUN go mod download

# Copie o código-fonte da aplicação
COPY . .

# Compile a aplicação
RUN go build -o main .

# Etapa final
FROM golang:1.22

# Defina o diretório de trabalho dentro do contêiner
WORKDIR /app

# Copie o binário da etapa de construção
COPY --from=builder /app/main .

# Exponha a porta em que a aplicação será executada
EXPOSE 8888

# Defina o comando para iniciar a aplicação
CMD ["./main"]
