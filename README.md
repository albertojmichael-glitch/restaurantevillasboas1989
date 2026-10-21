# Pizzaria Tycoon (C# Console + Mouse TUI)

Simulador de gerenciamento de restaurante em modo texto com suporte a cliques de mouse no terminal, desenvolvido em C# (.NET).

## Mecânicas Principais
- **5 Níveis de Progressão:** De *Iniciante* (Pizzaria) até *Restaurante Elite Global* (servindo Escargot e Baiacu).
- **Montagem de Pedidos:** Respeite o ingrediente base fixo, a ordem exata pedida pelo cliente e as restrições (*"sem ingrediente X"*).
- **Auditoria de Entregas:** Inspecione caixas com 7 itens verificando itens intrusos, erros de digitação, fornecedor, data e cidade (São Paulo, Curitiba e Rio de Janeiro).
- **Sistema Bancário:** Empréstimos com prazo de 12 meses e acréscimo de 1/3 de juros.
- **Tribunal e Processos:** Eventos judiciais onde a chance de vitória (sorteada entre 30% e 70%) é revelada antes de escolher entre fazer acordo ou ir a julgamento.
- **Variável `diversao`:** Sorteada entre 0 e 100 a cada nova *run*.

## Como Executar
```bash
dotnet run

---
## 3. Como Testar e Subir para o GitHub

Abra o terminal (PowerShell ou Prompt de Comando) dentro da pasta `PizzariaTycoon` onde você salvou os arquivos e execute:


```bash
dotnet run

# Para inicializar o repositório Git e subir para o seu GitHub:
git init
git add .
git commit -m "feat: sistema completo Pizzaria Tycoon em C# com suporte a mouse"
git branch -M main
git remote add origin https://github.com/SEU-USUARIO/NOME-DO-SEU-REPO.git
git push -u origin ma