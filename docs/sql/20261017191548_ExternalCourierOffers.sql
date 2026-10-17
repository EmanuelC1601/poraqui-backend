BEGIN TRANSACTION;
CREATE TABLE [CourierAvailabilities] (
    [CourierUserId] uniqueidentifier NOT NULL,
    [ZoneId] uniqueidentifier NULL,
    [IsAvailable] bit NOT NULL,
    [UpdatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CourierAvailabilities] PRIMARY KEY ([CourierUserId]),
    CONSTRAINT [FK_CourierAvailabilities_AspNetUsers_CourierUserId] FOREIGN KEY ([CourierUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CourierAvailabilities_DeliveryZones_ZoneId] FOREIGN KEY ([ZoneId]) REFERENCES [DeliveryZones] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ExternalDeliveryOffers] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [CourierUserId] uniqueidentifier NOT NULL,
    [ZoneId] uniqueidentifier NOT NULL,
    [PickupAddress] nvarchar(500) NOT NULL,
    [DestinationAddress] nvarchar(500) NOT NULL,
    [Remuneration] decimal(18,2) NOT NULL,
    [Conditions] nvarchar(1000) NULL,
    [OfferedAtUtc] datetimeoffset NOT NULL,
    [ExpiresAtUtc] datetimeoffset NOT NULL,
    [RespondedAtUtc] datetimeoffset NULL,
    [Status] nvarchar(20) NOT NULL,
    [Version] bigint NOT NULL,
    CONSTRAINT [PK_ExternalDeliveryOffers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExternalDeliveryOffers_AspNetUsers_CourierUserId] FOREIGN KEY ([CourierUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ExternalDeliveryOffers_DeliveryZones_ZoneId] FOREIGN KEY ([ZoneId]) REFERENCES [DeliveryZones] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ExternalDeliveryOffers_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_CourierAvailabilities_ZoneId] ON [CourierAvailabilities] ([ZoneId]);

CREATE INDEX [IX_ExternalDeliveryOffers_CourierUserId_Status_ExpiresAtUtc] ON [ExternalDeliveryOffers] ([CourierUserId], [Status], [ExpiresAtUtc]);

CREATE UNIQUE INDEX [IX_ExternalDeliveryOffers_OrderId] ON [ExternalDeliveryOffers] ([OrderId]) WHERE [Status] = 'Accepted';

CREATE INDEX [IX_ExternalDeliveryOffers_ZoneId] ON [ExternalDeliveryOffers] ([ZoneId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017191548_ExternalCourierOffers', N'9.0.10');

COMMIT;
GO
