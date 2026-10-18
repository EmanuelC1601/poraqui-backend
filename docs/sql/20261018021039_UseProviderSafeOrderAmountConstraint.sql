BEGIN TRANSACTION;
ALTER TABLE [Orders] DROP CONSTRAINT [CK_Orders_NonNegativeAmountsAndValidVersion];

ALTER TABLE [Orders] ADD CONSTRAINT [CK_Orders_NonNegativeAmountsAndValidVersion] CHECK ([DeliveryFee] >= 0 AND [Total] >= 0 AND ([CourierPaymentAmount] IS NULL OR [CourierPaymentAmount] >= 0) AND [Version] > 0);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261018021039_UseProviderSafeOrderAmountConstraint', N'10.0.13');

COMMIT;
GO

