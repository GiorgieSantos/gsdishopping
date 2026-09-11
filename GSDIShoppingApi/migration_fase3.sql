-- Migração incremental da Fase 3 — só ADITIVA: cria as duas tabelas novas
-- do painel de administração (AdminUsers, PointsAdjustments). Não toca em
-- Users, Stores, PointTransactions, Campaigns, Coupons ou Promotions —
-- nada do que você já tem precisa ser recriado desta vez.
--
-- Rode com: mysql -u root -p gsdishopping_app < migration_fase3.sql

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
    PointsDelta INT NOT NULL,
    Reason VARCHAR(500) NOT NULL,
    AdminUserId INT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_PointsAdjustments_Users
        FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_PointsAdjustments_AdminUsers
        FOREIGN KEY (AdminUserId) REFERENCES AdminUsers(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
