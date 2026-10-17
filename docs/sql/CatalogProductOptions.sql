BEGIN TRANSACTION;
CREATE TABLE [OrderLineOptions] (
    [Id] uniqueidentifier NOT NULL,
    [ProductOptionId] uniqueidentifier NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [PriceDelta] decimal(18,2) NOT NULL,
    [OrderLineId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_OrderLineOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderLineOptions_OrderLines_OrderLineId] FOREIGN KEY ([OrderLineId]) REFERENCES [OrderLines] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ProductOptionGroups] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [MinSelections] int NOT NULL,
    [MaxSelections] int NOT NULL,
    [IsAvailable] bit NOT NULL,
    CONSTRAINT [PK_ProductOptionGroups] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductOptionGroups_CatalogProducts_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [CatalogProducts] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ProductOptions] (
    [Id] uniqueidentifier NOT NULL,
    [GroupId] uniqueidentifier NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [PriceDelta] decimal(18,2) NOT NULL,
    [IsAvailable] bit NOT NULL,
    CONSTRAINT [PK_ProductOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductOptions_ProductOptionGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [ProductOptionGroups] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_OrderLineOptions_OrderLineId] ON [OrderLineOptions] ([OrderLineId]);

CREATE UNIQUE INDEX [IX_ProductOptionGroups_ProductId_Name] ON [ProductOptionGroups] ([ProductId], [Name]);

CREATE UNIQUE INDEX [IX_ProductOptions_GroupId_Name] ON [ProductOptions] ([GroupId], [Name]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017194824_CatalogProductOptions', N'9.0.10');

COMMIT;
GO

