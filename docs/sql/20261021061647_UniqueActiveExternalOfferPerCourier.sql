BEGIN TRANSACTION;
CREATE UNIQUE INDEX [IX_ExternalDeliveryOffers_OrderId_CourierUserId] ON [ExternalDeliveryOffers] ([OrderId], [CourierUserId]) WHERE [Status] = 'Offered';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261021061647_UniqueActiveExternalOfferPerCourier', N'10.0.13');

COMMIT;
GO

