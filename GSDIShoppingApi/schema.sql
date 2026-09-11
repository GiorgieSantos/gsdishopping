-- Schema equivalente ao definido em Models/ + Data/GSDIShoppingDbContext.cs.
-- Use isto para bootstrap rápido do banco, OU rode `dotnet ef database
-- update` (que gera exatamente isto a partir do código) — os dois
-- caminhos chegam no mesmo lugar, use o que preferir.
DROP TABLE Users;
CREATE TABLE IF NOT EXISTS Users (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Email VARCHAR(255) NOT NULL,
    -- Só dígitos (DDD + número), sem máscara — normalizado no cadastro.
    Phone VARCHAR(11) NOT NULL,
    -- Só dígitos (11 caracteres), sem máscara — normalizado no cadastro.
    Cpf VARCHAR(11) NOT NULL,
    -- 'Masculino', 'Feminino' ou 'Outro'.
    Sexo VARCHAR(20) NOT NULL,
    DataNascimento DATE NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY UX_Users_Email (Email),
    UNIQUE KEY UX_Users_Cpf (Cpf)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Stores (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    -- Só dígitos (14 caracteres), sem máscara — normalizado no cadastro.
    -- É o que a checagem de elegibilidade usa para saber se uma nota pontua.
    Cnpj VARCHAR(14) NOT NULL,
    Floor VARCHAR(100) NULL,
    LogoUrl VARCHAR(500) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY UX_Stores_Cnpj (Cnpj)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS PointTransactions (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    StoreId INT NOT NULL,
    AccessKey VARCHAR(44) NOT NULL,
    TotalValue DECIMAL(10,2) NOT NULL,
    PointsEarned INT NOT NULL,
    -- Status: 0 = Approved, 1 = RejectedDuplicate, 2 = RejectedInvalid,
    --         3 = Pending (Fase 4, aguardando NfceValidationWorker),
    --         4 = PendingReview (Fase 4, aguardando revisão manual)
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
    -- Texto livre: "20% OFF", "Leve 2 pague 1" etc.
    DiscountLabel VARCHAR(100) NOT NULL,
    -- NULL = promoção do shopping como um todo, não de uma loja específica.
    StoreId INT NULL,
    ImageUrl VARCHAR(500) NULL,
    StartsAt DATETIME NULL,
    EndsAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Promotions_Stores
        FOREIGN KEY (StoreId) REFERENCES Stores(Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS AdminUsers (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Email VARCHAR(255) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY UX_AdminUsers_Email (Email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS PointsAdjustments (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    -- Positivo = crédito de pontos, negativo = débito/correção.
    PointsDelta INT NOT NULL,
    Reason VARCHAR(500) NOT NULL,
    AdminUserId INT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_PointsAdjustments_Users
        FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_PointsAdjustments_AdminUsers
        FOREIGN KEY (AdminUserId) REFERENCES AdminUsers(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Fase 4: parâmetros de negócio configuráveis pelo painel (ver
-- Models/SystemParameter.cs e migration_fase4.sql).
CREATE TABLE IF NOT EXISTS SystemParameters (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    `Key` VARCHAR(100) NOT NULL,
    `Value` VARCHAR(500) NOT NULL,
    Description VARCHAR(1000) NOT NULL,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedByAdminId INT NULL,
    UNIQUE KEY UX_SystemParameters_Key (`Key`),
    CONSTRAINT FK_SystemParameters_AdminUsers
        FOREIGN KEY (UpdatedByAdminId) REFERENCES AdminUsers(Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
