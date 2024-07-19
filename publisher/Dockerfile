# Use uma imagem base oficial do Node.js
FROM node:22

# Crie um diretório de trabalho
WORKDIR /app

# Copie o package.json e o package-lock.json
COPY package*.json ./

# Instale as dependências
RUN npm install

# Copie o restante do código da aplicação
COPY . .

# Exponha a porta em que a aplicação será executada
EXPOSE 8080

# Comando para rodar a aplicação
CMD ["node", "app.js"]
