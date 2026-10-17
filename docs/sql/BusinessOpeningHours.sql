Build started...
Build succeeded.
BEGIN TRANSACTION;
ALTER TABLE [Businesses] ADD [HasConfiguredOpeningHours] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Businesses] ADD [TimeZoneId] nvarchar(100) NOT NULL DEFAULT N'UTC';

CREATE TABLE [BusinessOpeningHours] (
    [BusinessId] uniqueidentifier NOT NULL,
    [Day] int NOT NULL,
    [IsClosed] bit NOT NULL,
    [OpensAt] time NULL,
    [ClosesAt] time NULL,
    CONSTRAINT [PK_BusinessOpeningHours] PRIMARY KEY ([BusinessId], [Day]),
    CONSTRAINT [FK_BusinessOpeningHours_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017200001_BusinessOpeningHours', N'9.0.10');

COMMIT;
GO


