BEGIN TRANSACTION;
ALTER TABLE [ProductOptions] ADD CONSTRAINT [CK_ProductOptions_NonNegativePriceDelta] CHECK ([PriceDelta] >= 0);

ALTER TABLE [ProductOptionGroups] ADD CONSTRAINT [CK_ProductOptionGroups_SelectionBounds] CHECK ([MinSelections] >= 0 AND [MaxSelections] >= 1 AND [MinSelections] <= [MaxSelections]);

ALTER TABLE [Orders] ADD CONSTRAINT [CK_Orders_NonNegativeAmountsAndValidVersion] CHECK ([DeliveryFee] >= 0 AND [Total] >= [DeliveryFee] AND ([CourierPaymentAmount] IS NULL OR [CourierPaymentAmount] >= 0) AND [Version] > 0);

ALTER TABLE [OrderLines] ADD CONSTRAINT [CK_OrderLines_NonNegativeAmountsAndPositiveQuantity] CHECK ([UnitPrice] >= 0 AND [LineTotal] >= 0 AND [Quantity] > 0);

ALTER TABLE [ManualRefundCases] ADD CONSTRAINT [CK_ManualRefundCases_ValidAmounts] CHECK ([RequestedAmount] >= 0 AND ([RefundedAmount] IS NULL OR ([RefundedAmount] >= 0 AND [RefundedAmount] <= [RequestedAmount])));

ALTER TABLE [ExternalDeliveryOffers] ADD CONSTRAINT [CK_ExternalDeliveryOffers_ValidRemunerationVersionAndAcceptance] CHECK ([Remuneration] >= 0 AND [Version] > 0 AND ([Status] <> 'Accepted' OR [ProtectedPickupCode] IS NOT NULL));

ALTER TABLE [DeliveryRates] ADD CONSTRAINT [CK_DeliveryRates_NonNegativeFee] CHECK ([Fee] >= 0);

ALTER TABLE [CourierAvailabilities] ADD CONSTRAINT [CK_CourierAvailabilities_ZoneWhenAvailable] CHECK ([IsAvailable] = 0 OR [ZoneId] IS NOT NULL);

ALTER TABLE [CatalogProducts] ADD CONSTRAINT [CK_CatalogProducts_NonNegativePrice] CHECK ([Price] >= 0);

ALTER TABLE [BusinessOpeningHours] ADD CONSTRAINT [CK_BusinessOpeningHours_ClosedOrValidHours] CHECK (([IsClosed] = 1 AND [OpensAt] IS NULL AND [ClosesAt] IS NULL) OR ([IsClosed] = 0 AND [OpensAt] IS NOT NULL AND [ClosesAt] IS NOT NULL AND [OpensAt] < [ClosesAt]));

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261018020605_EnforceSqlDomainInvariants', N'10.0.13');

COMMIT;
GO

