USE master;
GO

-- 1. Crear Base de Datos de Prueba
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'AnomalyTestDb')
BEGIN
    CREATE DATABASE AnomalyTestDb;
END
GO

USE AnomalyTestDb;
GO

-- 2. Esquema y Datos Sintéticos con PII
CREATE TABLE dbo.customers (
    customer_id INT IDENTITY(1,1) PRIMARY KEY,
    full_name VARCHAR(100),
    national_id VARCHAR(20),
    credit_card VARCHAR(20),
    email VARCHAR(100),
    created_at DATETIME DEFAULT GETDATE()
);

CREATE TABLE dbo.transactions (
    transaction_id INT IDENTITY(1,1) PRIMARY KEY,
    customer_id INT FOREIGN KEY REFERENCES dbo.customers(customer_id),
    amount DECIMAL(18,2),
    transaction_date DATETIME DEFAULT GETDATE()
);

CREATE TABLE dbo.employees (
    employee_id INT IDENTITY(1,1) PRIMARY KEY,
    first_name VARCHAR(50),
    last_name VARCHAR(50),
    salary DECIMAL(18,2)
);

-- Inserción de Datos Sintéticos
INSERT INTO dbo.customers (full_name, national_id, credit_card, email)
VALUES 
('Juan Perez', 'V-12345678', '4532-XXXX-XXXX-8811', 'juan@example.com'),
('Maria Rodriguez', 'V-87654321', '4111-XXXX-XXXX-2233', 'maria@example.com');

INSERT INTO dbo.employees (first_name, last_name, salary)
VALUES ('Carlos', 'Gomez', 4500.00), ('Ana', 'Lopez', 5200.00);

-- 3. Crear Logins y Usuarios con Diferentes Privilegios
CREATE LOGIN app_service WITH PASSWORD = 'AppPassword123!';
CREATE USER app_service FOR LOGIN app_service;
ALTER ROLE db_datareader ADD MEMBER app_service;
ALTER ROLE db_datawriter ADD MEMBER app_service;

CREATE LOGIN read_only WITH PASSWORD = 'ReadOnlyPassword123!';
CREATE USER read_only FOR LOGIN read_only;
ALTER ROLE db_datareader ADD MEMBER read_only;

CREATE LOGIN analyst_user WITH PASSWORD = 'AnalystPassword123!';
CREATE USER analyst_user FOR LOGIN analyst_user;
ALTER ROLE db_datareader ADD MEMBER analyst_user;
GO

-- 4. Configurar Server Audit
USE master;
GO

IF EXISTS (SELECT * FROM sys.server_audits WHERE name = 'DAM_Server_Audit')
BEGIN
    ALTER SERVER AUDIT [DAM_Server_Audit] WITH (STATE = OFF);
    DROP SERVER AUDIT [DAM_Server_Audit];
END
GO

CREATE SERVER AUDIT [DAM_Server_Audit]
TO FILE 
(   FILEPATH = '/var/opt/mssql/audit/',
    MAXSIZE = 100 MB,
    MAX_ROLLOVER_FILES = 10,
    RESERVE_DISK_SPACE = OFF
)
WITH
(   QUEUE_DELAY = 1000,
    ON_FAILURE = CONTINUE
);
GO

ALTER SERVER AUDIT [DAM_Server_Audit] WITH (STATE = ON);
GO

-- 5. Configurar Database Audit Specification
USE AnomalyTestDb;
GO

IF EXISTS (SELECT * FROM sys.database_audit_specifications WHERE name = 'DAM_Database_Audit_Spec')
BEGIN
    ALTER DATABASE AUDIT SPECIFICATION [DAM_Database_Audit_Spec] WITH (STATE = OFF);
    DROP DATABASE AUDIT SPECIFICATION [DAM_Database_Audit_Spec];
END
GO

CREATE DATABASE AUDIT SPECIFICATION [DAM_Database_Audit_Spec]
FOR SERVER AUDIT [DAM_Server_Audit]
ADD (SELECT, INSERT, UPDATE, DELETE ON DATABASE::AnomalyTestDb BY PUBLIC),
ADD (SCHEMA_OBJECT_CHANGE_GROUP),
ADD (DATABASE_PRINCIPAL_CHANGE_GROUP),
ADD (FAILED_DATABASE_AUTHENTICATION_GROUP),
ADD (SUCCESSFUL_DATABASE_AUTHENTICATION_GROUP)
WITH (STATE = ON);
GO