#!/bin/bash
# Script para criar um novo cliente
# Uso: ./criar-cliente.sh "Nome da Empresa" "email@admin.com" "senha123"

EMPRESA="$1"
EMAIL="$2"
SENHA="$3"

if [ -z "$EMPRESA" ] || [ -z "$EMAIL" ] || [ -z "$SENHA" ]; then
    echo "Uso: ./criar-cliente.sh 'Nome da Empresa' 'email@admin.com' 'senha123'"
    exit 1
fi

# Gerar slug a partir do nome
SLUG=$(echo "$EMPRESA" | iconv -t ascii//TRANSLIT 2>/dev/null || echo "$EMPRESA" | sed 's/[áàã]/a/g;s/[éê]/e/g;s/[íì]/i/g;s/[óòõ]/o/g;s/[úù]/u/g;s/ç/c/g')
SLUG=$(echo "$SLUG" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9 -]//g' | sed 's/ /-/g' | sed 's/--*/-/g' | sed 's/^-//;s/-$//')

DB_PATH="./data/${SLUG}.db"

if [ -f "$DB_PATH" ]; then
    echo "Erro: Banco de dados ja existe para '$SLUG'"
    exit 1
fi

echo "Criando cliente: $EMPRESA"
echo "Slug: $SLUG"
echo "Email admin: $EMAIL"
echo "Banco: $DB_PATH"

# Copiar banco template ou deixar o backend criar automaticamente
touch "$DB_PATH"

echo ""
echo "====================================="
echo "Cliente criado com sucesso!"
echo "====================================="
echo "URL do painel: https://seudominio.com/login"
echo "URL do catalogo: https://seudominio.com/catalogo/$SLUG"
echo "Email: $EMAIL"
echo "Senha: $SENHA"
echo ""
echo "IMPORTANTE: Na primeira execucao, o backend criara as tabelas automaticamente."
echo "Depois acesse o painel e configure a loja."
