-- Migração incremental da Fase 2 — só ADITIVA: cria as três tabelas novas
-- do catálogo (Campaigns, Coupons, Promotions). Não toca em Users, Stores
-- ou PointTransactions — nada do que você já tem (login, lojas, extrato de
-- pontos) precisa ser recriado desta vez.
--
-- Rode com: mysql -u root -p gsdishopping_app < migration_fase2.sql

CREATE TABLE IF NOT EXISTS Campaigns (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Title VARCHAR(255) NOT NULL,
    Description VARCHAR(2000) NOT NULL,
    ImageUrl VARCHAR(500) NULL,
    StartsAt DATETIME NULL,
    EndsAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Coupons (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Title VARCHAR(255) NOT NULL,
    Description VARCHAR(2000) NOT NULL,
    PointsCost INT NOT NULL,
    CampaignId INT NOT NULL,
    ImageUrl VARCHAR(500) NULL,
    ExpiresAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Coupons_Campaigns
        FOREIGN KEY (CampaignId) REFERENCES Campaigns(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Promotions (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Title VARCHAR(255) NOT NULL,
    Description VARCHAR(2000) NOT NULL,
    DiscountLabel VARCHAR(100) NOT NULL,
    StoreId INT NULL,
    ImageUrl VARCHAR(500) NULL,
    StartsAt DATETIME NULL,
    EndsAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Promotions_Stores
        FOREIGN KEY (StoreId) REFERENCES Stores(Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
