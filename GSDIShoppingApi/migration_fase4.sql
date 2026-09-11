-- Migração incremental da Fase 4 — aditiva: cria a tabela nova de
-- parâmetros configuráveis (SystemParameters). Não recria nem altera o
-- schema de nenhuma tabela existente: PointTransactions.Status continua
-- sendo a mesma coluna TINYINT de sempre, só ganhando dois valores novos
-- no código (3 = Pending, 4 = PendingReview — ver Models/ReceiptStatus.cs),
-- o que não exige nenhuma mudança de DDL.
--
-- Rode com: mysql -u root -p gsdishopping_app < migration_fase4.sql

CREATE TABLE IF NOT EXISTS SystemParameters (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    -- Identificador estável usado pelo código (ex.: "CpfDivergenteAction").
    -- O painel só edita o Value de chaves já existentes; quem cria uma
    -- chave nova é o seed em Program.cs, na primeira vez que o código
    -- passa a conhecer aquele parâmetro.
    `Key` VARCHAR(100) NOT NULL,
    `Value` VARCHAR(500) NOT NULL,
    Description VARCHAR(1000) NOT NULL,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedByAdminId INT NULL,
    UNIQUE KEY UX_SystemParameters_Key (`Key`),
    CONSTRAINT FK_SystemParameters_AdminUsers
        FOREIGN KEY (UpdatedByAdminId) REFERENCES AdminUsers(Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- O parâmetro em si (CpfDivergenteAction) é criado automaticamente no
-- primeiro start da API depois desta migração (ver o seed no Program.cs)
-- — não precisa de INSERT manual aqui.
