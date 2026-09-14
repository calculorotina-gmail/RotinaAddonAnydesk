# AnyDeskMonitor / RotinaAddonAnydesk

Sistema profissional e completo em **C# / .NET 8** para monitorização centralizada de computadores Windows que possuem o AnyDesk instalado.

O agente em segundo plano chama-se **RotinaAddonAnydesk**, funcionando simultaneamente como **Windows Service** e **Tray Application** (ícone na barra de tarefas do Windows com comunicação por Named Pipes IPC).

---

## 🛡️ Princípio de Conformidade e Legitimidade

O sistema **não utiliza** engenharia reversa, APIs privadas, interceptação de tráfego de rede, manipulação de processos, captura de credenciais ou bypass de limitações do AnyDesk. 

A identificação do AnyDesk ID é feita de forma estritamente **legítima e não-intrusiva** através da leitura dos ficheiros de configuração locais padrão (`system.conf` / `user.conf`) disponibilizados pelo sistema operativo no computador administrado. Caso o ID não esteja disponível, o campo permanece `null` e o sistema funciona normalmente. A arquitetura está preparada com a interface `IAnyDeskProvider` para suportar futuramente a API oficial do AnyDesk (`OfficialAnyDeskApiProvider`).

---

## 🚀 Arquitetura do Sistema

```text
                         ┌─────────────────────┐
                         │     PostgreSQL      │
                         │                     │
                         │ Computers           │
                         │ Agents              │
                         │ AnyDesk IDs         │
                         │ Remote IDs          │
                         │ Sessions            │
                         │ Events              │
                         │ Audit               │
                         └──────────▲──────────┘
                                    │
                               EF Core
                                    │
                         ┌──────────┴──────────┐
                         │    ASP.NET API      │
                         │                     │
                         │ Auth (JWT)          │
                         │ Agents              │
                         │ Computers           │
                         │ Remote IDs          │
                         │ Sessions            │
                         │ SignalR Hub         │
                         └───────▲───────┬─────┘
                                 │       │
                              SignalR   HTTPS
                                 │       │
                    ┌────────────┘       └─────────────┐
                    │                                  │
             ┌──────┴─────────┐                ┌───────┴────────┐
             │ Blazor Web     │                │ Windows Agent  │
             │ Admin Panel    │                │                │
             └────────────────┘                │ Windows       │
                                               │ Service       │
                                               │                │
                                               │ Heartbeat     │
                                               │ Computer Info │
                                               │ AnyDesk Info  │
                                               └───────▲────────┘
                                                       │
                                                  Named Pipes
                                                       │
                                               ┌───────┴────────┐
                                               │ Tray           │
                                               │ Rotina         │
                                               │ AnyDesk       │
                                               └────────────────┘
```

---

## 🛠️ Tecnologias Utilizadas

- **Linguagem**: C# (.NET 8)
- **API Central**: ASP.NET Core Web API, JWT Authentication, Swagger/OpenAPI
- **Comunicação em Tempo Real**: ASP.NET Core SignalR
- **Painel Administrativo**: Blazor Server (Dark/Light Mode, Responsivo)
- **Agente Windows**: .NET Worker Service + Windows Service + System Tray (IPC Named Pipes)
- **Base de Dados**: PostgreSQL com Entity Framework Core
- **Instalador**: Inno Setup (`ISCC.exe`)
- **Automação**: PowerShell (`build.ps1`)
- **Containerização**: Docker & Docker Compose
- **Testes Unitários**: xUnit & Moq

---

## 📦 Estrutura da Solution

```text
AnyDeskMonitor.sln

src/
├── AnyDeskMonitor.Domain           # Entidades, Enums e Provider Legítimo AnyDesk
├── AnyDeskMonitor.Shared           # DTOs compartilhados, Enums e Constantes SignalR
├── AnyDeskMonitor.Application      # Lógica de Negócios e Serviços da Aplicação
├── AnyDeskMonitor.Infrastructure   # EF Core, PostgreSQL, JWT Token Generator e Hasher
├── AnyDeskMonitor.Api              # API ASP.NET Core REST, SignalR Hub e Background Monitor
├── AnyDeskMonitor.Web              # Painel Administrativo Blazor com SignalR
└── AnyDeskMonitor.Agent            # Agente Windows Service + Tray App (RotinaAddonAnydesk)

tests/
└── AnyDeskMonitor.Tests            # Testes unitários xUnit (Agente, Provider, API, Serviços)

build.ps1                           # Script de build automático e geração de executáveis/instalador
setup.iss                           # Script de configuração do Inno Setup
docker-compose.yml                  # Orquestração Docker (PostgreSQL + API + Web)
.env.example                        # Exemplo de variáveis de ambiente
README.md                           # Documentação técnica do projeto
```

---

## ⚡ Como Compilar e Gerar Instaladores Automáticos

Para executar o pipeline completo de compilação, validação, testes unitários, publicação de binários e geração do instalador Inno Setup:

```powershell
.\build.ps1
```

O script `build.ps1` irá:
1. Limpar compilações anteriores;
2. Restaurar dependências NuGet;
3. Validar o ambiente (.NET SDK e Inno Setup);
4. Compilar a Solution em modo Release;
5. Executar os testes unitários via `dotnet test`;
6. Publicar a API Central, Dashboard Web e o Agente Windows;
7. Gerar os executáveis portáteis `RotinaAddonAnydesk.exe` e `Aplicativo.exe`;
8. Executar o Inno Setup (`ISCC.exe`) para gerar `RotinaAddonAnydesk-Setup.exe` e `aplicativo-Setup.exe`;
9. Validar os ficheiros e gravar todo o registo em `build.txt`;
10. Exibir a mensagem final **BUILD CONCLUÍDO COM SUCESSO**.

---

## 🐳 Execução via Docker Compose

Para iniciar a base de dados PostgreSQL, a API Central e o Dashboard Web num ambiente Docker:

```bash
# Copiar o ficheiro de variáveis de ambiente
cp .env.example .env

# Iniciar os serviços via Docker Compose
docker-compose up -d --build
```

Aceda aos serviços:
- **Dashboard Web**: http://localhost:7001
- **API Central / Swagger**: http://localhost:5000/swagger

---

## 💻 Instalação e Execução Manual do Agente Windows

### 1. Iniciar como Aplicação Tray (Modo Interativo)

```cmd
RotinaAddonAnydesk.exe --tray
```

### 2. Instalar como Serviço Windows (`RotinaAddonAnydesk`)

Abra o PowerShell ou Prompt de Comando como Administrador:

```cmd
sc.exe create RotinaAddonAnydesk binPath= "C:\Caminho\Para\RotinaAddonAnydesk.exe --service" start= auto displayName= "Rotina AnyDesk Monitor Agent"
sc.exe failure RotinaAddonAnydesk reset= 86400 actions= restart/60000/restart/60000/restart/60000
sc.exe start RotinaAddonAnydesk
```

---

## 🔑 Credenciais Padrão e Autenticação

Ao iniciar o sistema pela primeira vez, os seguintes utilizadores são criados automaticamente:

| Utilizador | Palavra-passe | Perfil (Role) | Permissões |
| :--- | :--- | :--- | :--- |
| `admin` | `admin123` | `Administrator` | Acesso completo a utilizadores, computadores e configurações |
| `operator` | `operator123` | `Operator` | Gestão de computadores, agentes e IDs remotos |
| `viewer` | `viewer123` | `Viewer` | Acesso exclusivo de visualização |

---

## 🧪 Testes Unitários

Para executar os testes unitários manualmente:

```bash
dotnet test tests/AnyDeskMonitor.Tests/AnyDeskMonitor.Tests.csproj --configuration Release
```

---

## 📝 Licença e Conformidade

Este software foi projetado para auditoria e gestão de computadores corporativos administrados de forma totalmente transparente e segura.
# RotinaAddonAnydesk
