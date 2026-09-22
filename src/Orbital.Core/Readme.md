# 🧠 Orbital.Core

O projeto **Orbital.Core** é o coração lógico e computacional do ecossistema de análise de mercado. Este projeto encapsula as **regras de negócio, lógica de processamento intensivo e consultas complexas**, servindo como uma camada de abstração sólida entre os dados (MarketData) e as aplicações clientes.

## 🎯 Objetivo Arquitetural

A principal responsabilidade do `Orbital.Core` é processar e tratar os dados de mercado, evitando a sobrecarga de processamento nas camadas de interface de usuário (como o `OrbitalUI` feito em .NET MAUI). 

### Por que separar a lógica visual da lógica de negócio?
1. **Performance e Fluidez na UI:** Aplicações focadas no mercado financeiro exigem baixíssima latência visual (60fps+). Qualquer agregação, cálculo de algoritmos preditivos, ou consulta pesada feita na *Main/UI Thread* pode causar engasgos (*stutters*) na renderização de gráficos.
2. **Reuso de Código:** Concentrando as regras aqui, podemos reutilizar a mesma lógica em diferentes frontends no futuro (ex: um painel Web Blazor, um bot Telegram ou uma CLI) sem reescrever regras de mercado.
3. **Escalabilidade (Processamento Distribuído):** A lógica pesada em um serviço/núcleo apartado permite que as consultas ao banco temporal (QuestDB) ou a memória distribuída sejam consolidadas e cacheadas eficientemente, entregando apenas o "resultado final pronto" para a UI.

## 🛠️ O que compõe o Orbital.Core?
À medida que o projeto evolui, este repositório deverá conter:
* **Casos de Uso (Use Cases / Services):** Serviços que consultam, agregam e filtram as métricas do banco de dados (ex: `VolumeAtPriceCalculator`, `OrderFlowAnalyzer`).
* **Modelos de Domínio:** Entidades ricas e Value Objects agnósticos à infraestrutura (sem dependências de banco ou frameworks de UI).
* **Interfaces (Contratos):** As definições de portas (Ports) para acesso a repositórios externos e serviços de mensageria, favorecendo a injeção de dependência e testabilidade.

## 🏎️ Fluxo de Dados e Integração

O fluxo ideal de arquitetura do sistema segue a premissa de baixo acoplamento:
1. `Orbital.Database` (ou repositórios externos) fornece os dados brutos e massivos.
2. **`Orbital.Core`** consome esses dados, processa agregações, aplica filtros de negócio (ex: expurgo de leilões, cálculo de VWAP) e emite DTOs simplificados.
3. `OrbitalUI` (Aplicação MAUI) recebe via injetores ou streams os objetos leves prontos para renderização, garantindo fluidez e rápida atualização visual.

---

## ▶️ Como Executar e Utilizar

O `Orbital.Core` é desenvolvido como uma **Class Library** que contém *BackgroundServices* (Serviços em Segundo Plano) e lógicas de processamento que precisam ser hospedados por um aplicativo principal (ex: Console App, Web API, ou projeto MAUI).

### 1. Requisitos
- .NET 8 ou superior instalado.
- Instância do QuestDB rodando (necessária para os módulos analíticos como o SMC Engine).

### 2. Injetando no Projeto Host
Para dar vida ao `Orbital.Core` na sua aplicação host (por exemplo, na `MarketData.API` ou em um Worker Service), você deve registrar suas dependências no `Program.cs`. 

Criamos o método de extensão `AddOrbitalCore` para facilitar esse processo:

```csharp
// No Program.cs do seu projeto Host (ex: Web API ou Worker)
using Orbital.Core;

var builder = WebApplication.CreateBuilder(args);

// String de conexão para a API Npgsql acessar o QuestDB
var questDbConnection = builder.Configuration.GetConnectionString("QuestDB") 
    ?? "Host=localhost;Port=8812;Database=qdb;Username=admin;Password=quest";

// Registra todos os módulos de análise pesada, como o SMCEngine (SMC) e ZoneInterestManager
builder.Services.AddOrbitalCore(questDbConnection);

var app = builder.Build();
app.Run();
```

### 3. Build do Projeto
Sempre que fizer alterações nas regras de negócio, certifique-se de que o projeto compila isoladamente:
```bash
cd Orbital.Core
dotnet build
```

> **Atenção:** Ao iniciar a aplicação Host que chama `AddOrbitalCore`, os *BackgroundServices* integrados (como o `SMCEngine`) começarão a rodar e a varrer automaticamente o QuestDB a cada intervalo estipulado. Mantenha os logs em `Information` ativados para acompanhar as detecções na saída do console.
