-- Migração incremental da Fase 1 — mantém a tabela Users intacta (login,
-- cadastro de teste que você já validou continuam funcionando).
--
-- O que a Fase 1 mudou de verdade no banco:
--   1) nova tabela Stores;
--   2) PointTransactions.StoreId deixou de ser texto livre e virou
--      referência (INT) para Stores.
--
-- PointTransactions ainda não tinha uso real na Fase 0 (a checagem de
-- elegibilidade não existia — StoreId era só um texto solto, sem vínculo
-- com nada), então o caminho mais simples e seguro é recriar só essa
-- tabela, em vez de tentar converter dados de teste que não significam
-- nada no novo modelo. Se você tiver transações nela que queira preservar
-- de verdade, me avise antes de rodar isto.
--
-- Rode com: mysql -u root -p gsdishopping_app < migration_fase1.sql

CREATE TABLE IF NOT EXISTS Stores (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    -- Só dígitos (14 caracteres), sem máscara — normalizado no cadastro.
    Cnpj VARCHAR(14) NOT NULL,
    Floor VARCHAR(100) NULL,
    LogoUrl VARCHAR(500) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY UX_Stores_Cnpj (Cnpj)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS PointTransactions;

CREATE TABLE PointTransactions (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    StoreId INT NOT NULL,
    AccessKey VARCHAR(44) NOT NULL,
    TotalValue DECIMAL(10,2) NOT NULL,
    PointsEarned INT NOT NULL,
    -- Status: 0 = Approved, 1 = RejectedDuplicate, 2 = RejectedInvalid
    Status TINYINT NOT NULL,
    RejectionReason VARCHAR(500) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ValidatedAt DATETIME NULL,
    UNIQUE KEY UX_PointTransactions_AccessKey (AccessKey),
    CONSTRAINT FK_PointTransactions_Users
        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_PointTransactions_Stores
        FOREIGN KEY (StoreId) REFERENCES Stores(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
