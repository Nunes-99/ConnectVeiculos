-- =============================================================
-- ConnectVeiculos - Schema do Banco de Dados SQLite
-- Gerado automaticamente pelo Entity Framework Core
-- =============================================================

-- Tabela de Usuarios
CREATE TABLE IF NOT EXISTS Usuario (
    UsuId INTEGER PRIMARY KEY AUTOINCREMENT,
    UsuNome TEXT NOT NULL,
    UsuCPF TEXT,
    UsuRG TEXT,
    UsuEmail TEXT NOT NULL,
    UsuSenha TEXT NOT NULL,
    UsuFuncao TEXT,
    UsuSts INTEGER NOT NULL DEFAULT 1
);

-- Tabela de Lojas
CREATE TABLE IF NOT EXISTS Loja (
    LojId INTEGER PRIMARY KEY AUTOINCREMENT,
    LojNome TEXT NOT NULL,
    LojLogradouro TEXT,
    LojNumero TEXT,
    LojBairro TEXT,
    LojCidade TEXT,
    LojEstado TEXT,
    LojCEP TEXT,
    LojComplemento TEXT,
    LojEmail TEXT,
    LojTel1 TEXT,
    LojTel2 TEXT,
    LojWhatsApp TEXT,
    LojImg TEXT,
    LojCNPJ TEXT,
    LojIE TEXT,
    LojSts INTEGER NOT NULL DEFAULT 1
);

-- Tabela de Relacao Loja-Usuario
CREATE TABLE IF NOT EXISTS LojaUsuario (
    LojUsuId INTEGER PRIMARY KEY AUTOINCREMENT,
    R_UsuId INTEGER NOT NULL,
    R_LojId INTEGER NOT NULL,
    UsuAcs TEXT,
    FOREIGN KEY (R_UsuId) REFERENCES Usuario(UsuId),
    FOREIGN KEY (R_LojId) REFERENCES Loja(LojId)
);

-- Tabela de Categorias de Veiculos
CREATE TABLE IF NOT EXISTS Categoria (
    CatId INTEGER PRIMARY KEY AUTOINCREMENT,
    CatNome TEXT NOT NULL,
    CatDesc TEXT,
    CatSts INTEGER NOT NULL DEFAULT 1
);

-- Tabela de Veiculos
CREATE TABLE IF NOT EXISTS Veiculo (
    VeiId INTEGER PRIMARY KEY AUTOINCREMENT,
    R_LojId INTEGER NOT NULL,
    R_CatId INTEGER,
    VeiMarca TEXT,
    VeiModelo TEXT,
    VeiAno INTEGER,
    VeiPlaca TEXT,
    VeiChassi TEXT,
    VeiCor TEXT,
    VeiKm INTEGER,
    VeiPreco REAL,
    VeiDtEntrada TEXT,
    VeiSts TEXT DEFAULT 'D',
    VeiSitSts TEXT,
    VeiPrecoCompra REAL,
    VeiObservacao TEXT,
    FOREIGN KEY (R_LojId) REFERENCES Loja(LojId),
    FOREIGN KEY (R_CatId) REFERENCES Categoria(CatId)
);

-- Tabela de Niveis de Acesso
CREATE TABLE IF NOT EXISTS Acesso (
    AcsId INTEGER PRIMARY KEY AUTOINCREMENT,
    AcsNome TEXT NOT NULL,
    AcsDesc TEXT,
    AcsSts INTEGER NOT NULL DEFAULT 1
);

-- Tabela de Permissoes (Usuario-Acesso)
CREATE TABLE IF NOT EXISTS Permissao (
    UsuAcsId INTEGER PRIMARY KEY AUTOINCREMENT,
    R_UsuId INTEGER NOT NULL,
    R_AcsId INTEGER NOT NULL,
    AcsTp TEXT,
    FOREIGN KEY (R_UsuId) REFERENCES Usuario(UsuId),
    FOREIGN KEY (R_AcsId) REFERENCES Acesso(AcsId)
);

-- Tabela de Caracteristicas
CREATE TABLE IF NOT EXISTS Caracteristica (
    CarId INTEGER PRIMARY KEY AUTOINCREMENT,
    CarNome TEXT NOT NULL
);

-- Tabela de Relacao Veiculo-Caracteristica
CREATE TABLE IF NOT EXISTS VeiculoCaracteristica (
    VeiCarId INTEGER PRIMARY KEY AUTOINCREMENT,
    R_VeiId INTEGER NOT NULL,
    R_CarId INTEGER NOT NULL,
    FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId),
    FOREIGN KEY (R_CarId) REFERENCES Caracteristica(CarId)
);

-- Tabela de Observacoes
CREATE TABLE IF NOT EXISTS Observacao (
    ObsId INTEGER PRIMARY KEY AUTOINCREMENT,
    ObsNome TEXT NOT NULL
);

-- Tabela de Relacao Veiculo-Observacao
CREATE TABLE IF NOT EXISTS VeiculoObservacao (
    VeiObsId INTEGER PRIMARY KEY AUTOINCREMENT,
    R_VeiId INTEGER NOT NULL,
    R_ObsId INTEGER NOT NULL,
    FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId),
    FOREIGN KEY (R_ObsId) REFERENCES Observacao(ObsId)
);

-- Tabela de Imagens do Veiculo
CREATE TABLE IF NOT EXISTS VeiculoImagem (
    ImgId INTEGER PRIMARY KEY AUTOINCREMENT,
    R_VeiId INTEGER NOT NULL,
    ImgCaminho TEXT,
    ImgOrdem INTEGER,
    FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId)
);

-- Tabela de Vendas
CREATE TABLE IF NOT EXISTS Venda (
    VenId INTEGER PRIMARY KEY AUTOINCREMENT,
    R_VeiId INTEGER,
    R_UsuId INTEGER,
    VenMarca TEXT,
    VenModelo TEXT,
    VenChassi TEXT,
    VenAno INTEGER,
    VenData TEXT,
    VenValor REAL,
    VenComissaoPorc REAL,
    VenComissaoValor REAL,
    FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId),
    FOREIGN KEY (R_UsuId) REFERENCES Usuario(UsuId)
);

-- Tabela de Test Drives
CREATE TABLE IF NOT EXISTS TestDrive (TdrId INTEGER PRIMARY KEY AUTOINCREMENT, R_VeiId INTEGER, R_LojId INTEGER, TdrNomeCliente TEXT NOT NULL, TdrTelefone TEXT, TdrEmail TEXT, TdrDataAgendamento TEXT, TdrHorario TEXT, TdrObservacao TEXT, TdrStatus TEXT DEFAULT 'P', TdrDtCriacao TEXT, FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId));

-- Tabela de Despesas de Veiculos
CREATE TABLE IF NOT EXISTS VeiculoDespesa (DesId INTEGER PRIMARY KEY AUTOINCREMENT, R_VeiId INTEGER NOT NULL, DesTipo TEXT NOT NULL, DesDescricao TEXT, DesValor REAL, DesDtDespesa TEXT, DesDtCriacao TEXT, FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId));

-- Tabela de Leads
CREATE TABLE IF NOT EXISTS Lead (LeaId INTEGER PRIMARY KEY AUTOINCREMENT, R_VeiId INTEGER, R_LojId INTEGER, LeaNomeCliente TEXT, LeaTelefone TEXT, LeaEmail TEXT, LeaOrigem TEXT, LeaStatus TEXT DEFAULT 'NOVO', LeaObservacao TEXT, LeaDtCriacao TEXT, FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId));

-- Tabela de Favoritos
CREATE TABLE IF NOT EXISTS Favorito (FavId INTEGER PRIMARY KEY AUTOINCREMENT, R_VeiId INTEGER NOT NULL, FavEmail TEXT NOT NULL, FavNome TEXT, FavTelefone TEXT, FavDtCriacao TEXT, FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId));
CREATE UNIQUE INDEX IF NOT EXISTS idx_favorito_email_veiculo ON Favorito(FavEmail, R_VeiId);

-- Tabela de Recuperacao de Senha
CREATE TABLE IF NOT EXISTS RecuperacaoSenha (
    RecId INTEGER PRIMARY KEY AUTOINCREMENT,
    RecUsuId INTEGER NOT NULL,
    RecToken TEXT NOT NULL,
    RecDataCriacao TEXT,
    RecDataExpiracao TEXT,
    RecUtilizado INTEGER DEFAULT 0,
    FOREIGN KEY (RecUsuId) REFERENCES Usuario(UsuId)
);
CREATE INDEX IF NOT EXISTS idx_recuperacao_token ON RecuperacaoSenha(RecToken);
CREATE INDEX IF NOT EXISTS idx_recuperacao_usuario ON RecuperacaoSenha(RecUsuId);

-- =============================================================
-- Indices para melhor performance
-- =============================================================
CREATE INDEX IF NOT EXISTS idx_usuario_email ON Usuario(UsuEmail);
CREATE INDEX IF NOT EXISTS idx_veiculo_loja ON Veiculo(R_LojId);
CREATE INDEX IF NOT EXISTS idx_veiculo_categoria ON Veiculo(R_CatId);
CREATE INDEX IF NOT EXISTS idx_veiculo_placa ON Veiculo(VeiPlaca);
CREATE INDEX IF NOT EXISTS idx_lojausuario_usuario ON LojaUsuario(R_UsuId);
CREATE INDEX IF NOT EXISTS idx_lojausuario_loja ON LojaUsuario(R_LojId);
