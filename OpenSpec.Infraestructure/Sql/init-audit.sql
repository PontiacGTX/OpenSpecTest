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

-- 2. Esquema y Datos Sint�ticos con PII
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

-- Inserci�n de Datos Sint�ticos
INSERT INTO dbo.customers (full_name, national_id, credit_card, email)
VALUES 
('Juan Perez', 'V-12345678', '4532-XXXX-XXXX-8811', 'juan@example.com'),
('Maria Rodriguez', 'V-87654321', '4111-XXXX-XXXX-2233', 'maria@example.com');

INSERT INTO dbo.customers (full_name, national_id, credit_card, email)
VALUES
('Luis Martinez', 'V-10000001', '4000-XXXX-XXXX-0001', 'luis.martinez@example.com'),
('Sofia Torres', 'V-10000002', '4000-XXXX-XXXX-0002', 'sofia.torres@example.com'),
('Diego Ramirez', 'V-10000003', '4000-XXXX-XXXX-0003', 'diego.ramirez@example.com'),
('Laura Hernandez', 'V-10000004', '4000-XXXX-XXXX-0004', 'laura.hernandez@example.com'),
('Miguel Castillo', 'V-10000005', '4000-XXXX-XXXX-0005', 'miguel.castillo@example.com'),
('Elena Vargas', 'V-10000006', '4000-XXXX-XXXX-0006', 'elena.vargas@example.com'),
('Andres Silva', 'V-10000007', '4000-XXXX-XXXX-0007', 'andres.silva@example.com'),
('Patricia Rojas', 'V-10000008', '4000-XXXX-XXXX-0008', 'patricia.rojas@example.com'),
('Roberto Mendoza', 'V-10000009', '4000-XXXX-XXXX-0009', 'roberto.mendoza@example.com'),
('Carmen Flores', 'V-10000010', '4000-XXXX-XXXX-0010', 'carmen.flores@example.com'),
('Jorge Navarro', 'V-10000011', '4000-XXXX-XXXX-0011', 'jorge.navarro@example.com'),
('Gabriela Ortiz', 'V-10000012', '4000-XXXX-XXXX-0012', 'gabriela.ortiz@example.com'),
('Fernando Cruz', 'V-10000013', '4000-XXXX-XXXX-0013', 'fernando.cruz@example.com'),
('Valeria Molina', 'V-10000014', '4000-XXXX-XXXX-0014', 'valeria.molina@example.com'),
('Ricardo Paredes', 'V-10000015', '4000-XXXX-XXXX-0015', 'ricardo.paredes@example.com'),
('Natalia Campos', 'V-10000016', '4000-XXXX-XXXX-0016', 'natalia.campos@example.com');

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

-- Principales de prueba asociados a los 16 clientes para crear perfiles auditables.
CREATE USER customer_user_01 WITHOUT LOGIN;
CREATE USER customer_user_02 WITHOUT LOGIN;
CREATE USER customer_user_03 WITHOUT LOGIN;
CREATE USER customer_user_04 WITHOUT LOGIN;
CREATE USER customer_user_05 WITHOUT LOGIN;
CREATE USER customer_user_06 WITHOUT LOGIN;
CREATE USER customer_user_07 WITHOUT LOGIN;
CREATE USER customer_user_08 WITHOUT LOGIN;
CREATE USER customer_user_09 WITHOUT LOGIN;
CREATE USER customer_user_10 WITHOUT LOGIN;
CREATE USER customer_user_11 WITHOUT LOGIN;
CREATE USER customer_user_12 WITHOUT LOGIN;
CREATE USER customer_user_13 WITHOUT LOGIN;
CREATE USER customer_user_14 WITHOUT LOGIN;
CREATE USER customer_user_15 WITHOUT LOGIN;
CREATE USER customer_user_16 WITHOUT LOGIN;
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

-- 6. Escenarios de prueba para el motor de anomalías
-- Estas consultas generan eventos auditables al iniciar el entorno.

-- A. Actividad normal de bajo volumen
SELECT TOP 10 customer_id, full_name FROM dbo.customers ORDER BY customer_id;
SELECT TOP 15 employee_id, first_name, last_name FROM dbo.employees ORDER BY employee_id;
SELECT TOP 5 transaction_id, amount FROM dbo.transactions ORDER BY transaction_date DESC;
SELECT TOP 20 customer_id, email FROM dbo.customers WHERE customer_id <= 20;
SELECT TOP 10 employee_id, salary FROM dbo.employees WHERE salary < 6000;
SELECT TOP 8 transaction_id, customer_id FROM dbo.transactions WHERE amount < 1000;

-- B. Exfiltración o volumen anómalo: supera ampliamente el baseline de 42 filas
SELECT TOP 5000 o1.name, o2.name
FROM sys.all_objects AS o1 CROSS JOIN sys.all_objects AS o2;
SELECT TOP 6000 o1.object_id, o2.object_id
FROM sys.all_objects AS o1 CROSS JOIN sys.all_objects AS o2;
SELECT TOP 7000 c1.full_name, c2.email
FROM dbo.customers AS c1 CROSS JOIN dbo.customers AS c2
              CROSS JOIN sys.all_objects AS o;
SELECT TOP 8000 t.transaction_id, t.amount, c.email
FROM dbo.transactions AS t
JOIN dbo.customers AS c ON c.customer_id = t.customer_id
CROSS JOIN sys.all_objects AS o;
SELECT TOP 9000 o1.name, o2.name, o3.name
FROM sys.all_objects AS o1
CROSS JOIN sys.all_objects AS o2
CROSS JOIN sys.all_objects AS o3;
SELECT TOP 10000 c.customer_id, c.national_id, c.credit_card, c.email
FROM dbo.customers AS c CROSS JOIN sys.all_objects AS o;

-- C. Consultas con patrones sospechosos para la detección semántica
SELECT * FROM dbo.customers WHERE name = 'users' OR '1' = '1';
SELECT * FROM dbo.customers WHERE customer_id = 1 OR 1 = 1;
SELECT customer_id, full_name, email FROM dbo.customers
WHERE email LIKE '%@example.com' UNION SELECT 1, 'probe', 'probe@example.com';
SELECT * FROM dbo.employees WHERE salary > 0 OR 'x' = 'x' -- bypass de filtro
;
SELECT national_id, credit_card, email FROM dbo.customers
WHERE customer_id IN (SELECT customer_id FROM dbo.transactions);
SELECT TOP 1000 * FROM dbo.customers UNION ALL SELECT TOP 1000 * FROM dbo.customers;

-- D. Acciones destructivas controladas: se auditan, pero los datos se revierten
BEGIN TRANSACTION;
UPDATE dbo.employees SET salary = salary + 100000 WHERE employee_id = 1;
DELETE TOP (1) FROM dbo.transactions WHERE transaction_id > 0;
UPDATE dbo.customers SET email = 'anomaly-test@example.com' WHERE customer_id = 1;
DELETE TOP (1) FROM dbo.customers WHERE customer_id > 0;
ROLLBACK TRANSACTION;

-- E. Procedimiento sensible. Requiere que xp_cmdshell este habilitado en SQL Server.
-- Si se habilita para una prueba controlada, esta sentencia debe producir SensitiveSpExec.
-- EXEC master.dbo.xp_cmdshell 'echo anomaly-test';

-- F. Casos que deben ejecutarse desde el cliente de pruebas, no desde este script:
-- * UnknownHost: abrir la conexion usando otra IP o Workstation ID.
-- * OffHours: ejecutar una consulta fuera del horario configurado del baseline.
-- * BruteForce: generar logins fallidos y luego uno exitoso con varias conexiones.
-- * AuditTamper: probar ALTER SERVER AUDIT ... WITH (STATE = OFF) solo en un entorno aislado.