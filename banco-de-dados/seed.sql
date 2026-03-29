-- =============================================================
-- ConnectVeiculos - Dados Iniciais (Seed)
-- Execute este script apos criar o banco para ter dados de teste
-- =============================================================

-- Niveis de Acesso
INSERT INTO Acesso (AcsNome, AcsDesc, AcsSts) VALUES ('Administrador', 'Acesso total ao sistema', 1);
INSERT INTO Acesso (AcsNome, AcsDesc, AcsSts) VALUES ('Gerente', 'Acesso gerencial', 1);
INSERT INTO Acesso (AcsNome, AcsDesc, AcsSts) VALUES ('Vendedor', 'Acesso de vendas', 1);
INSERT INTO Acesso (AcsNome, AcsDesc, AcsSts) VALUES ('Visualizador', 'Apenas visualizacao', 1);

-- Categorias de Veiculos
INSERT INTO Categoria (CatNome, CatDesc, CatSts) VALUES ('Sedan', 'Carros sedan', 1);
INSERT INTO Categoria (CatNome, CatDesc, CatSts) VALUES ('Hatch', 'Carros hatchback', 1);
INSERT INTO Categoria (CatNome, CatDesc, CatSts) VALUES ('SUV', 'Veiculos utilitarios esportivos', 1);
INSERT INTO Categoria (CatNome, CatDesc, CatSts) VALUES ('Pickup', 'Caminhonetes', 1);
INSERT INTO Categoria (CatNome, CatDesc, CatSts) VALUES ('Esportivo', 'Carros esportivos', 1);
INSERT INTO Categoria (CatNome, CatDesc, CatSts) VALUES ('Van', 'Vans e furgoes', 1);
INSERT INTO Categoria (CatNome, CatDesc, CatSts) VALUES ('Motocicleta', 'Motos', 1);

-- Caracteristicas comuns de veiculos
INSERT INTO Caracteristica (CarNome) VALUES ('Ar Condicionado');
INSERT INTO Caracteristica (CarNome) VALUES ('Direcao Hidraulica');
INSERT INTO Caracteristica (CarNome) VALUES ('Direcao Eletrica');
INSERT INTO Caracteristica (CarNome) VALUES ('Vidros Eletricos');
INSERT INTO Caracteristica (CarNome) VALUES ('Travas Eletricas');
INSERT INTO Caracteristica (CarNome) VALUES ('Alarme');
INSERT INTO Caracteristica (CarNome) VALUES ('Airbag');
INSERT INTO Caracteristica (CarNome) VALUES ('ABS');
INSERT INTO Caracteristica (CarNome) VALUES ('Cambio Automatico');
INSERT INTO Caracteristica (CarNome) VALUES ('Banco de Couro');
INSERT INTO Caracteristica (CarNome) VALUES ('Teto Solar');
INSERT INTO Caracteristica (CarNome) VALUES ('Sensor de Estacionamento');
INSERT INTO Caracteristica (CarNome) VALUES ('Camera de Re');
INSERT INTO Caracteristica (CarNome) VALUES ('Multimidia');
INSERT INTO Caracteristica (CarNome) VALUES ('Bluetooth');
INSERT INTO Caracteristica (CarNome) VALUES ('Rodas de Liga');

-- Loja de exemplo
INSERT INTO Loja (LojNome, LojLogradouro, LojNumero, LojBairro, LojCidade, LojEstado, LojCEP, LojEmail, LojTel1, LojCNPJ, LojSts)
VALUES ('Connect Veiculos Matriz', 'Avenida Brasil', '1000', 'Centro', 'Sao Paulo', 'SP', '01310-100', 'contato@connectveiculos.com.br', '(11) 3000-0000', '00.000.000/0001-00', 1);

-- Usuario administrador padrao (senha: admin123)
INSERT INTO Usuario (UsuNome, UsuEmail, UsuSenha, UsuFuncao, UsuSts)
VALUES ('Administrador', 'admin@connectveiculos.com.br', 'admin123', 'Administrador', 1);

-- Vincular usuario a loja
INSERT INTO LojaUsuario (R_UsuId, R_LojId, UsuAcs) VALUES (1, 1, 'A');

-- Dar permissao de admin ao usuario
INSERT INTO Permissao (R_UsuId, R_AcsId, AcsTp) VALUES (1, 1, 'A');

-- Veiculos de exemplo
INSERT INTO Veiculo (R_LojId, R_CatId, VeiMarca, VeiModelo, VeiAno, VeiPlaca, VeiCor, VeiKm, VeiPreco, VeiSts, VeiPrecoCompra)
VALUES (1, 1, 'Toyota', 'Corolla XEi 2.0', 2023, 'ABC-1234', 'Prata', 15000, 145000.00, 'D', 130000.00);

INSERT INTO Veiculo (R_LojId, R_CatId, VeiMarca, VeiModelo, VeiAno, VeiPlaca, VeiCor, VeiKm, VeiPreco, VeiSts, VeiPrecoCompra)
VALUES (1, 2, 'Volkswagen', 'Polo Highline', 2022, 'DEF-5678', 'Branco', 28000, 95000.00, 'D', 85000.00);

INSERT INTO Veiculo (R_LojId, R_CatId, VeiMarca, VeiModelo, VeiAno, VeiPlaca, VeiCor, VeiKm, VeiPreco, VeiSts, VeiPrecoCompra)
VALUES (1, 3, 'Jeep', 'Compass Limited', 2023, 'GHI-9012', 'Preto', 12000, 185000.00, 'D', 165000.00);

INSERT INTO Veiculo (R_LojId, R_CatId, VeiMarca, VeiModelo, VeiAno, VeiPlaca, VeiCor, VeiKm, VeiPreco, VeiSts, VeiPrecoCompra)
VALUES (1, 4, 'Toyota', 'Hilux SRX', 2022, 'JKL-3456', 'Prata', 45000, 285000.00, 'R', 260000.00);

-- Caracteristicas dos veiculos
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (1, 1);  -- Corolla - Ar Condicionado
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (1, 3);  -- Corolla - Direcao Eletrica
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (1, 4);  -- Corolla - Vidros Eletricos
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (1, 7);  -- Corolla - Airbag
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (1, 8);  -- Corolla - ABS
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (1, 14); -- Corolla - Multimidia

INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (2, 1);  -- Polo - Ar Condicionado
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (2, 3);  -- Polo - Direcao Eletrica
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (2, 4);  -- Polo - Vidros Eletricos
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (2, 15); -- Polo - Bluetooth

INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (3, 1);  -- Compass - Ar Condicionado
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (3, 9);  -- Compass - Cambio Automatico
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (3, 10); -- Compass - Banco de Couro
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (3, 11); -- Compass - Teto Solar
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (3, 12); -- Compass - Sensor de Estacionamento
INSERT INTO VeiculoCaracteristica (R_VeiId, R_CarId) VALUES (3, 13); -- Compass - Camera de Re

-- =============================================================
-- Fim dos dados iniciais
-- =============================================================
