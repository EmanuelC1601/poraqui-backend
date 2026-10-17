BEGIN TRANSACTION;
CREATE TABLE [OfflineDeliverySyncRecords] (
    [Id] uniqueidentifier NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [PayloadHash] nvarchar(64) NOT NULL,
    [ClientCapturedAtUtc] datetimeoffset NOT NULL,
    [ServerAcceptedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_OfflineDeliverySyncRecords] PRIMARY KEY ([ActorUserId], [Id]),
    CONSTRAINT [FK_OfflineDeliverySyncRecords_AspNetUsers_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OfflineDeliverySyncRecords_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_OfflineDeliverySyncRecords_OrderId] ON [OfflineDeliverySyncRecords] ([OrderId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017192726_OfflineDeliveryCommandSync', N'9.0.10');

COMMIT;
GO
