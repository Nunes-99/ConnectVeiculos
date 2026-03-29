# Planejamento - Back-end ConnectVeiculos

## Arquitetura: Clean Architecture (Baseado no ConnectX)

O projeto seguirá a mesma arquitetura do ConnectX, com 4 camadas principais:

```
ConnectVeiculos/
├── ConnectVeiculos.API/              # Camada de Apresentação (Controllers)
├── ConnectVeiculos.Application/      # Camada de Aplicação (UseCases, InputModels, ViewModels)
├── ConnectVeiculos.Core/             # Camada de Domínio (Entities, Interfaces, DTOs, Enums)
├── ConnectVeiculos.Infrastructure/   # Camada de Infraestrutura (Repositories, DbContext, Operations)
└── ConnectVeiculos.sln               # Solution
```

---

## 1. Estrutura de Projetos

### 1.1. ConnectVeiculos.Core (Class Library)
```
ConnectVeiculos.Core/
├── Entities/
│   ├── Usuarios/
│   │   └── Usuario.cs
│   ├── Lojas/
│   │   └── Loja.cs
│   ├── LojasUsuarios/
│   │   └── LojaUsuario.cs
│   ├── Categorias/
│   │   └── Categoria.cs
│   ├── Veiculos/
│   │   └── Veiculo.cs
│   ├── Acessos/
│   │   └── Acesso.cs
│   ├── Permissoes/
│   │   └── Permissao.cs
│   ├── Caracteristicas/
│   │   └── Caracteristica.cs
│   ├── VeiculosCaracteristicas/
│   │   └── VeiculoCaracteristica.cs
│   ├── Observacoes/
│   │   └── Observacao.cs
│   ├── VeiculosObservacoes/
│   │   └── VeiculoObservacao.cs
│   ├── VeiculosImagens/
│   │   └── VeiculoImagem.cs
│   └── Vendas/
│       └── Venda.cs
├── DTOs/
│   ├── Usuarios/
│   ├── Lojas/
│   ├── Veiculos/
│   └── Vendas/
├── Enums/
│   └── Common/
│       └── Status.cs
├── Exceptions/
│   ├── Usuarios/
│   │   └── UsuarioException.cs
│   └── ...
└── Interfaces/
    └── Database/
        ├── Common/
        │   ├── IUnitOfWork.cs
        │   └── IUnitOfWorkFactory.cs
        ├── Repositories/
        │   ├── Usuarios/
        │   │   └── IUsuarioRepository.cs
        │   └── ...
        └── Operations/
            ├── Usuarios/
            │   └── IUsuarioOperation.cs
            └── ...
```

### 1.2. ConnectVeiculos.Application (Class Library)
```
ConnectVeiculos.Application/
├── InputModels/
│   ├── Usuarios/
│   │   └── UsuarioInputModel.cs
│   ├── Lojas/
│   │   └── LojaInputModel.cs
│   ├── Veiculos/
│   │   └── VeiculoInputModel.cs
│   └── ...
├── ViewModels/
│   ├── Usuarios/
│   │   ├── UsuarioViewModel.cs
│   │   └── UsuarioListViewModel.cs
│   ├── Lojas/
│   ├── Veiculos/
│   └── ...
├── Interfaces/
│   ├── Usuarios/
│   │   ├── ICadastrarUsuarioUseCase.cs
│   │   ├── IAtualizarUsuarioUseCase.cs
│   │   ├── IConsultarUsuarioPorIdUseCase.cs
│   │   ├── IConsultarUsuariosUseCase.cs
│   │   └── IInativarUsuarioUseCase.cs
│   └── ...
├── UseCases/
│   ├── Usuarios/
│   │   ├── CadastrarUsuarioUseCase.cs
│   │   ├── AtualizarUsuarioUseCase.cs
│   │   ├── ConsultarUsuarioPorIdUseCase.cs
│   │   ├── ConsultarUsuariosUseCase.cs
│   │   └── InativarUsuarioUseCase.cs
│   └── ...
└── Exceptions/
    └── InputModelException.cs
```

### 1.3. ConnectVeiculos.Infrastructure (Class Library)
```
ConnectVeiculos.Infrastructure/
├── Database/
│   ├── EntityFramework/
│   │   ├── Configurations/
│   │   │   ├── UsuarioConfiguration.cs
│   │   │   ├── LojaConfiguration.cs
│   │   │   ├── VeiculoConfiguration.cs
│   │   │   └── ...
│   │   ├── Repositories/
│   │   │   ├── UsuarioRepository.cs
│   │   │   └── ...
│   │   └── ConnectVeiculosDbContext.cs
│   ├── Operations/
│   │   ├── Usuarios/
│   │   │   └── UsuarioOperation.cs
│   │   └── ...
│   └── UnitOfWork/
│       ├── DbSession.cs
│       ├── UnitOfWork.cs
│       └── UnitOfWorkFactory.cs
└── DependencyInjection/
    └── InfrastructureServiceExtensions.cs
```

### 1.4. ConnectVeiculos.API (Web API)
```
ConnectVeiculos.API/
├── Controllers/
│   ├── UsuariosController.cs
│   ├── LojasController.cs
│   ├── CategoriasController.cs
│   ├── VeiculosController.cs
│   ├── AcessosController.cs
│   ├── PermissoesController.cs
│   ├── CaracteristicasController.cs
│   ├── ObservacoesController.cs
│   ├── VeiculosImagensController.cs
│   └── VendasController.cs
├── Properties/
│   └── launchSettings.json
├── appsettings.json
├── appsettings.Development.json
└── Program.cs
```

---

## 2. Entidades (Models) - Detalhamento

### 2.1. Usuario
```csharp
namespace ConnectVeiculos.Core.Entities.Usuarios
{
    public class Usuario
    {
        public int UsuId { get; private set; }           // PK
        public string UsuNome { get; private set; }      // VARCHAR(200)
        public string UsuCPF { get; private set; }       // VARCHAR(14)
        public string UsuRG { get; private set; }        // VARCHAR(20)
        public string UsuEmail { get; private set; }     // VARCHAR(255) - Login
        public string UsuSenha { get; private set; }     // VARCHAR(255) - Hash
        public string UsuFuncao { get; private set; }    // VARCHAR(100)
        public bool UsuSts { get; private set; }         // BIT - 0=Inativo/1=Ativo

        // Construtor, SetProperties, Validate...
    }
}
```

### 2.2. Loja
```csharp
namespace ConnectVeiculos.Core.Entities.Lojas
{
    public class Loja
    {
        public int LojId { get; private set; }           // PK
        public string LojNome { get; private set; }      // VARCHAR(200)
        public string LojLogradouro { get; private set; } // VARCHAR(255)
        public string LojNumero { get; private set; }    // VARCHAR(50)
        public string LojBairro { get; private set; }    // VARCHAR(150)
        public string LojCidade { get; private set; }    // VARCHAR(150)
        public string LojEstado { get; private set; }    // CHAR(2)
        public string LojCEP { get; private set; }       // VARCHAR(9)
        public string LojComplemento { get; private set; } // VARCHAR(255)
        public string LojEmail { get; private set; }     // VARCHAR(255)
        public string LojTel1 { get; private set; }      // VARCHAR(20)
        public string LojTel2 { get; private set; }      // VARCHAR(20)
        public string LojImg { get; private set; }       // VARCHAR(500)
        public string LojCNPJ { get; private set; }      // VARCHAR(18)
        public string LojIE { get; private set; }        // VARCHAR(20)
        public bool LojSts { get; private set; }         // BIT
    }
}
```

### 2.3. LojaUsuario (Relacionamento N:N)
```csharp
namespace ConnectVeiculos.Core.Entities.LojasUsuarios
{
    public class LojaUsuario
    {
        public int LojUsuId { get; private set; }   // PK
        public int R_UsuId { get; private set; }    // FK -> Usuario
        public int R_LojId { get; private set; }    // FK -> Loja
        public string UsuAcs { get; private set; }  // VARCHAR(1) - A=Admin, B=Básico

        // Navigation Properties
        public Usuario Usuario { get; private set; }
        public Loja Loja { get; private set; }
    }
}
```

### 2.4. Categoria
```csharp
namespace ConnectVeiculos.Core.Entities.Categorias
{
    public class Categoria
    {
        public int CatId { get; private set; }      // PK
        public string CatNome { get; private set; } // VARCHAR(100)
        public string CatDesc { get; private set; } // VARCHAR(255)
        public bool CatSts { get; private set; }    // BIT
    }
}
```

### 2.5. Veiculo
```csharp
namespace ConnectVeiculos.Core.Entities.Veiculos
{
    public class Veiculo
    {
        public int VeiId { get; private set; }           // PK
        public int R_LojId { get; private set; }         // FK -> Loja
        public int R_CatId { get; private set; }         // FK -> Categoria
        public string VeiMarca { get; private set; }     // VARCHAR(100)
        public string VeiModelo { get; private set; }    // VARCHAR(150)
        public short VeiAno { get; private set; }        // SMALLINT
        public string VeiPlaca { get; private set; }     // VARCHAR(10)
        public string VeiChassi { get; private set; }    // VARCHAR(20)
        public string VeiCor { get; private set; }       // VARCHAR(50)
        public int VeiKm { get; private set; }           // INT
        public decimal VeiPreco { get; private set; }    // DECIMAL(10,2)
        public DateTime VeiDtEntrada { get; private set; } // DATE
        public string VeiSts { get; private set; }       // VARCHAR(1) - D=Disponível
        public string VeiSitSts { get; private set; }    // VARCHAR(50) - Situação
        public decimal VeiPrecoCompra { get; private set; } // DECIMAL(10,2)

        // Navigation Properties
        public Loja Loja { get; private set; }
        public Categoria Categoria { get; private set; }
        public List<VeiculoCaracteristica> Caracteristicas { get; private set; }
        public List<VeiculoObservacao> Observacoes { get; private set; }
        public List<VeiculoImagem> Imagens { get; private set; }
    }
}
```

### 2.6. Acesso
```csharp
namespace ConnectVeiculos.Core.Entities.Acessos
{
    public class Acesso
    {
        public int AcsId { get; private set; }      // PK
        public string AcsNome { get; private set; } // VARCHAR(100)
        public string AcsDesc { get; private set; } // VARCHAR(255)
        public bool AcsSts { get; private set; }    // BIT
    }
}
```

### 2.7. Permissao (Relacionamento N:N Usuario-Acesso)
```csharp
namespace ConnectVeiculos.Core.Entities.Permissoes
{
    public class Permissao
    {
        public int UsuAcsId { get; private set; }   // PK
        public int R_UsuId { get; private set; }    // FK -> Usuario
        public int R_AcsId { get; private set; }    // FK -> Acesso
        public string AcsTp { get; private set; }   // VARCHAR(1) - L=Leitura, E=Escrita, T=Total

        // Navigation Properties
        public Usuario Usuario { get; private set; }
        public Acesso Acesso { get; private set; }
    }
}
```

### 2.8. Caracteristica
```csharp
namespace ConnectVeiculos.Core.Entities.Caracteristicas
{
    public class Caracteristica
    {
        public int CarId { get; private set; }      // PK
        public string CarNome { get; private set; } // VARCHAR(100)
        public bool CarSts { get; private set; }    // BIT
    }
}
```

### 2.9. VeiculoCaracteristica (Relacionamento N:N)
```csharp
namespace ConnectVeiculos.Core.Entities.VeiculosCaracteristicas
{
    public class VeiculoCaracteristica
    {
        public int VeiCarId { get; private set; }   // PK
        public int R_VeiId { get; private set; }    // FK -> Veiculo
        public int R_CarId { get; private set; }    // FK -> Caracteristica

        public Veiculo Veiculo { get; private set; }
        public Caracteristica Caracteristica { get; private set; }
    }
}
```

### 2.10. Observacao
```csharp
namespace ConnectVeiculos.Core.Entities.Observacoes
{
    public class Observacao
    {
        public int ObsId { get; private set; }       // PK
        public string ObsNome { get; private set; }  // VARCHAR(1000)
        public bool ObsSts { get; private set; }     // BIT
    }
}
```

### 2.11. VeiculoObservacao (Relacionamento N:N)
```csharp
namespace ConnectVeiculos.Core.Entities.VeiculosObservacoes
{
    public class VeiculoObservacao
    {
        public int VeiObsId { get; private set; }   // PK
        public int R_VeiId { get; private set; }    // FK -> Veiculo
        public int R_ObsId { get; private set; }    // FK -> Observacao

        public Veiculo Veiculo { get; private set; }
        public Observacao Observacao { get; private set; }
    }
}
```

### 2.12. VeiculoImagem
```csharp
namespace ConnectVeiculos.Core.Entities.VeiculosImagens
{
    public class VeiculoImagem
    {
        public int ImgId { get; private set; }       // PK
        public int R_VeiId { get; private set; }     // FK -> Veiculo
        public string ImgCaminho { get; private set; } // VARCHAR(500)
        public int ImgOrdem { get; private set; }    // INT
        public bool ImgSts { get; private set; }     // BIT

        public Veiculo Veiculo { get; private set; }
    }
}
```

### 2.13. Venda
```csharp
namespace ConnectVeiculos.Core.Entities.Vendas
{
    public class Venda
    {
        public int VenId { get; private set; }            // PK
        public int R_VeiId { get; private set; }          // FK -> Veiculo
        public int R_UsuId { get; private set; }          // FK -> Usuario (Vendedor)
        public DateTime VenDtVenda { get; private set; }  // DATETIME
        public string VenMarca { get; private set; }      // VARCHAR(100)
        public string VenModelo { get; private set; }     // VARCHAR(150)
        public short VenAno { get; private set; }         // SMALLINT
        public string VenChassi { get; private set; }     // VARCHAR(20)
        public decimal VenValor { get; private set; }     // DECIMAL(10,2)
        public decimal VenComissaoPorc { get; private set; } // DECIMAL(5,2)
        public decimal VenComissaoValor { get; private set; } // DECIMAL(10,2)

        public Veiculo Veiculo { get; private set; }
        public Usuario Vendedor { get; private set; }
    }
}
```

---

## 3. Controllers - Endpoints da API

### 3.1. UsuariosController
| Método | Rota | Ação | UseCase |
|--------|------|------|---------|
| GET | /api/usuarios | Listar todos | ConsultarUsuariosUseCase |
| GET | /api/usuarios/{id} | Buscar por ID | ConsultarUsuarioPorIdUseCase |
| POST | /api/usuarios | Criar | CadastrarUsuarioUseCase |
| PUT | /api/usuarios/{id} | Atualizar | AtualizarUsuarioUseCase |
| DELETE | /api/usuarios/{id} | Inativar | InativarUsuarioUseCase |

### 3.2. LojasController
| Método | Rota | Ação | UseCase |
|--------|------|------|---------|
| GET | /api/lojas | Listar todas | ConsultarLojasUseCase |
| GET | /api/lojas/{id} | Buscar por ID | ConsultarLojaPorIdUseCase |
| POST | /api/lojas | Criar | CadastrarLojaUseCase |
| PUT | /api/lojas/{id} | Atualizar | AtualizarLojaUseCase |
| DELETE | /api/lojas/{id} | Inativar | InativarLojaUseCase |

### 3.3. CategoriasController
| Método | Rota | Ação | UseCase |
|--------|------|------|---------|
| GET | /api/categorias | Listar todas | ConsultarCategoriasUseCase |
| GET | /api/categorias/{id} | Buscar por ID | ConsultarCategoriaPorIdUseCase |
| POST | /api/categorias | Criar | CadastrarCategoriaUseCase |
| PUT | /api/categorias/{id} | Atualizar | AtualizarCategoriaUseCase |
| DELETE | /api/categorias/{id} | Inativar | InativarCategoriaUseCase |

### 3.4. VeiculosController
| Método | Rota | Ação | UseCase |
|--------|------|------|---------|
| GET | /api/veiculos | Listar todos | ConsultarVeiculosUseCase |
| GET | /api/veiculos/{id} | Buscar por ID | ConsultarVeiculoPorIdUseCase |
| POST | /api/veiculos | Criar | CadastrarVeiculoUseCase |
| PUT | /api/veiculos/{id} | Atualizar | AtualizarVeiculoUseCase |
| DELETE | /api/veiculos/{id} | Inativar | InativarVeiculoUseCase |

### 3.5. AcessosController
| Método | Rota | Ação | UseCase |
|--------|------|------|---------|
| GET | /api/acessos | Listar todos | ConsultarAcessosUseCase |
| GET | /api/acessos/{id} | Buscar por ID | ConsultarAcessoPorIdUseCase |
| POST | /api/acessos | Criar | CadastrarAcessoUseCase |
| PUT | /api/acessos/{id} | Atualizar | AtualizarAcessoUseCase |
| DELETE | /api/acessos/{id} | Inativar | InativarAcessoUseCase |

### 3.6. PermissoesController
| Método | Rota | Ação | UseCase |
|--------|------|------|---------|
| GET | /api/permissoes | Listar todas | ConsultarPermissoesUseCase |
| GET | /api/permissoes/{id} | Buscar por ID | ConsultarPermissaoPorIdUseCase |
| POST | /api/permissoes | Criar | CadastrarPermissaoUseCase |
| PUT | /api/permissoes/{id} | Atualizar | AtualizarPermissaoUseCase |
| DELETE | /api/permissoes/{id} | Deletar | DeletarPermissaoUseCase |

### 3.7. CaracteristicasController
| Método | Rota | Ação | UseCase |
|--------|------|------|---------|
| GET | /api/caracteristicas | Listar todas | ConsultarCaracteristicasUseCase |
| GET | /api/caracteristicas/{id} | Buscar por ID | ConsultarCaracteristicaPorIdUseCase |
| POST | /api/caracteristicas | Criar | CadastrarCaracteristicaUseCase |
| PUT | /api/caracteristicas/{id} | Atualizar | AtualizarCaracteristicaUseCase |
| DELETE | /api/caracteristicas/{id} | Inativar | InativarCaracteristicaUseCase |

### 3.8. ObservacoesController
| Método | Rota | Ação | UseCase |
|--------|------|------|---------|
| GET | /api/observacoes | Listar todas | ConsultarObservacoesUseCase |
| GET | /api/observacoes/{id} | Buscar por ID | ConsultarObservacaoPorIdUseCase |
| POST | /api/observacoes | Criar | CadastrarObservacaoUseCase |
| PUT | /api/observacoes/{id} | Atualizar | AtualizarObservacaoUseCase |
| DELETE | /api/observacoes/{id} | Inativar | InativarObservacaoUseCase |

### 3.9. VeiculosImagensController
| Método | Rota | Ação | UseCase |
|--------|------|------|---------|
| GET | /api/veiculos/{veiculoId}/imagens | Listar imagens do veículo | ConsultarImagensVeiculoUseCase |
| POST | /api/veiculos/{veiculoId}/imagens | Adicionar imagem | CadastrarImagemVeiculoUseCase |
| PUT | /api/veiculos/imagens/{id} | Atualizar imagem | AtualizarImagemVeiculoUseCase |
| DELETE | /api/veiculos/imagens/{id} | Inativar imagem | InativarImagemVeiculoUseCase |

### 3.10. VendasController
| Método | Rota | Ação | UseCase |
|--------|------|------|---------|
| GET | /api/vendas | Listar todas | ConsultarVendasUseCase |
| GET | /api/vendas/{id} | Buscar por ID | ConsultarVendaPorIdUseCase |
| POST | /api/vendas | Registrar venda | CadastrarVendaUseCase |
| PUT | /api/vendas/{id} | Atualizar | AtualizarVendaUseCase |
| DELETE | /api/vendas/{id} | Cancelar | CancelarVendaUseCase |

---

## 4. Pacotes NuGet Necessários

### ConnectVeiculos.Core
- (Nenhum pacote externo necessário)

### ConnectVeiculos.Application
- (Nenhum pacote externo necessário)

### ConnectVeiculos.Infrastructure
- Microsoft.EntityFrameworkCore (8.0.x)
- Microsoft.EntityFrameworkCore.SqlServer (8.0.x)
- Microsoft.EntityFrameworkCore.Tools (8.0.x)
- Dapper (2.1.x)

### ConnectVeiculos.API
- Microsoft.AspNetCore.OpenApi (8.0.x)
- Swashbuckle.AspNetCore (6.5.x)

---

## 5. Script SQL para Criação do Banco (SSMS)

```sql
-- Criação do Banco de Dados
CREATE DATABASE ConnectVeiculosDB;
GO

USE ConnectVeiculosDB;
GO

-- Tabela Usuario
CREATE TABLE Usuario (
    UsuId INT IDENTITY(1,1) PRIMARY KEY,
    UsuNome VARCHAR(200) NOT NULL,
    UsuCPF VARCHAR(14),
    UsuRG VARCHAR(20),
    UsuEmail VARCHAR(255) NOT NULL,
    UsuSenha VARCHAR(255) NOT NULL,
    UsuFuncao VARCHAR(100),
    UsuSts BIT NOT NULL DEFAULT 1
);

-- Tabela Loja
CREATE TABLE Loja (
    LojId INT IDENTITY(1,1) PRIMARY KEY,
    LojNome VARCHAR(200) NOT NULL,
    LojLogradouro VARCHAR(255),
    LojNumero VARCHAR(50),
    LojBairro VARCHAR(150),
    LojCidade VARCHAR(150),
    LojEstado CHAR(2),
    LojCEP VARCHAR(9),
    LojComplemento VARCHAR(255),
    LojEmail VARCHAR(255),
    LojTel1 VARCHAR(20),
    LojTel2 VARCHAR(20),
    LojImg VARCHAR(500),
    LojCNPJ VARCHAR(18),
    LojIE VARCHAR(20),
    LojSts BIT NOT NULL DEFAULT 1
);

-- Tabela LojaUsuario (Relacionamento N:N)
CREATE TABLE LojaUsuario (
    LojUsuId INT IDENTITY(1,1) PRIMARY KEY,
    R_UsuId INT NOT NULL,
    R_LojId INT NOT NULL,
    UsuAcs VARCHAR(1),
    FOREIGN KEY (R_UsuId) REFERENCES Usuario(UsuId),
    FOREIGN KEY (R_LojId) REFERENCES Loja(LojId)
);

-- Tabela Categoria
CREATE TABLE Categoria (
    CatId INT IDENTITY(1,1) PRIMARY KEY,
    CatNome VARCHAR(100) NOT NULL,
    CatDesc VARCHAR(255),
    CatSts BIT NOT NULL DEFAULT 1
);

-- Tabela Veiculo
CREATE TABLE Veiculo (
    VeiId INT IDENTITY(1,1) PRIMARY KEY,
    R_LojId INT NOT NULL,
    R_CatId INT NOT NULL,
    VeiMarca VARCHAR(100),
    VeiModelo VARCHAR(150),
    VeiAno SMALLINT,
    VeiPlaca VARCHAR(10),
    VeiChassi VARCHAR(20),
    VeiCor VARCHAR(50),
    VeiKm INT,
    VeiPreco DECIMAL(10,2),
    VeiDtEntrada DATE,
    VeiSts VARCHAR(1),
    VeiSitSts VARCHAR(50),
    VeiPrecoCompra DECIMAL(10,2),
    FOREIGN KEY (R_LojId) REFERENCES Loja(LojId),
    FOREIGN KEY (R_CatId) REFERENCES Categoria(CatId)
);

-- Tabela Acesso
CREATE TABLE Acesso (
    AcsId INT IDENTITY(1,1) PRIMARY KEY,
    AcsNome VARCHAR(100) NOT NULL,
    AcsDesc VARCHAR(255),
    AcsSts BIT NOT NULL DEFAULT 1
);

-- Tabela Permissao (Relacionamento N:N Usuario-Acesso)
CREATE TABLE Permissao (
    UsuAcsId INT IDENTITY(1,1) PRIMARY KEY,
    R_UsuId INT NOT NULL,
    R_AcsId INT NOT NULL,
    AcsTp VARCHAR(1),
    FOREIGN KEY (R_UsuId) REFERENCES Usuario(UsuId),
    FOREIGN KEY (R_AcsId) REFERENCES Acesso(AcsId)
);

-- Tabela Caracteristica
CREATE TABLE Caracteristica (
    CarId INT IDENTITY(1,1) PRIMARY KEY,
    CarNome VARCHAR(100) NOT NULL,
    CarSts BIT NOT NULL DEFAULT 1
);

-- Tabela VeiculoCaracteristica (Relacionamento N:N)
CREATE TABLE VeiculoCaracteristica (
    VeiCarId INT IDENTITY(1,1) PRIMARY KEY,
    R_VeiId INT NOT NULL,
    R_CarId INT NOT NULL,
    FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId),
    FOREIGN KEY (R_CarId) REFERENCES Caracteristica(CarId)
);

-- Tabela Observacao
CREATE TABLE Observacao (
    ObsId INT IDENTITY(1,1) PRIMARY KEY,
    ObsNome VARCHAR(1000) NOT NULL,
    ObsSts BIT NOT NULL DEFAULT 1
);

-- Tabela VeiculoObservacao (Relacionamento N:N)
CREATE TABLE VeiculoObservacao (
    VeiObsId INT IDENTITY(1,1) PRIMARY KEY,
    R_VeiId INT NOT NULL,
    R_ObsId INT NOT NULL,
    FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId),
    FOREIGN KEY (R_ObsId) REFERENCES Observacao(ObsId)
);

-- Tabela VeiculoImagem
CREATE TABLE VeiculoImagem (
    ImgId INT IDENTITY(1,1) PRIMARY KEY,
    R_VeiId INT NOT NULL,
    ImgCaminho VARCHAR(500),
    ImgOrdem INT,
    ImgSts BIT NOT NULL DEFAULT 1,
    FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId)
);

-- Tabela Venda
CREATE TABLE Venda (
    VenId INT IDENTITY(1,1) PRIMARY KEY,
    R_VeiId INT NOT NULL,
    R_UsuId INT NOT NULL,
    VenDtVenda DATETIME NOT NULL,
    VenMarca VARCHAR(100),
    VenModelo VARCHAR(150),
    VenAno SMALLINT,
    VenChassi VARCHAR(20),
    VenValor DECIMAL(10,2),
    VenComissaoPorc DECIMAL(5,2),
    VenComissaoValor DECIMAL(10,2),
    FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId),
    FOREIGN KEY (R_UsuId) REFERENCES Usuario(UsuId)
);
GO
```

---

## 6. Ordem de Implementação Recomendada

### Fase 1 - Estrutura Base
1. Criar Solution e Projetos
2. Configurar referências entre projetos
3. Instalar pacotes NuGet
4. Criar DbContext e configuração de conexão

### Fase 2 - Entidades Independentes (sem FK)
1. Usuario
2. Loja
3. Categoria
4. Acesso
5. Caracteristica
6. Observacao

### Fase 3 - Entidades com FK simples
1. Veiculo (depende de Loja e Categoria)
2. VeiculoImagem (depende de Veiculo)

### Fase 4 - Tabelas de Relacionamento N:N
1. LojaUsuario
2. Permissao
3. VeiculoCaracteristica
4. VeiculoObservacao

### Fase 5 - Entidades Complexas
1. Venda (depende de Veiculo e Usuario)

### Fase 6 - Finalização
1. Configurar Injeção de Dependência
2. Testes unitários (opcional)
3. Documentação Swagger

---

## 7. Padrões do ConnectX a Seguir

### 7.1. Entidades
- Propriedades com `private set`
- Construtor vazio + Construtor com parâmetros
- Método `SetProperties()` para atribuição
- Método `Validate()` para validação
- Exceptions específicas por entidade

### 7.2. UseCases
- Uma interface por UseCase
- Método `Execute()` como ponto de entrada
- Injeção de Repository e UnitOfWork via construtor
- Transaction management: BeginTransaction → Create/Update → Commit

### 7.3. InputModels
- Validação no construtor
- Exceção `InputModelException` para erros de validação

### 7.4. ViewModels
- Classes simples com propriedades públicas
- Sem lógica de negócio

### 7.5. Controllers
- Injeção de UseCase via `[FromServices]`
- Retornos: `Ok()`, `NotFound()`, `BadRequest()`, `NoContent()`
- Validação de `InputModel is null`

### 7.6. Operations (Dapper)
- Queries SQL diretas
- Mapeamento manual com `Select()`
- Uso de `DbSession` para Connection/Transaction

### 7.7. Repositories (Entity Framework)
- CRUD básico
- Uso de DbContext

---

## 8. Comandos para Criar o Projeto

```bash
# Criar Solution
dotnet new sln -n ConnectVeiculos

# Criar projetos
dotnet new classlib -n ConnectVeiculos.Core -f net8.0
dotnet new classlib -n ConnectVeiculos.Application -f net8.0
dotnet new classlib -n ConnectVeiculos.Infrastructure -f net8.0
dotnet new webapi -n ConnectVeiculos.API -f net8.0

# Adicionar projetos à Solution
dotnet sln add ConnectVeiculos.Core/ConnectVeiculos.Core.csproj
dotnet sln add ConnectVeiculos.Application/ConnectVeiculos.Application.csproj
dotnet sln add ConnectVeiculos.Infrastructure/ConnectVeiculos.Infrastructure.csproj
dotnet sln add ConnectVeiculos.API/ConnectVeiculos.API.csproj

# Adicionar referências
cd ConnectVeiculos.Application
dotnet add reference ../ConnectVeiculos.Core/ConnectVeiculos.Core.csproj

cd ../ConnectVeiculos.Infrastructure
dotnet add reference ../ConnectVeiculos.Core/ConnectVeiculos.Core.csproj
dotnet add reference ../ConnectVeiculos.Application/ConnectVeiculos.Application.csproj

cd ../ConnectVeiculos.API
dotnet add reference ../ConnectVeiculos.Application/ConnectVeiculos.Application.csproj
dotnet add reference ../ConnectVeiculos.Infrastructure/ConnectVeiculos.Infrastructure.csproj

# Instalar pacotes
cd ../ConnectVeiculos.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Dapper

cd ../ConnectVeiculos.API
dotnet add package Swashbuckle.AspNetCore
```

---

## Conclusão

Este planejamento fornece uma visão completa de como estruturar o projeto back-end ConnectVeiculos seguindo os mesmos padrões do projeto ConnectX existente. A arquitetura Clean Architecture com separação em 4 camadas permite:

- **Manutenibilidade**: Código organizado e fácil de manter
- **Testabilidade**: Camadas independentes facilitam testes unitários
- **Escalabilidade**: Estrutura preparada para crescimento
- **Consistência**: Mesmo padrão do ConnectX existente

Quando estiver pronto para implementar, basta seguir a ordem de implementação recomendada na Fase 1 a 6.
