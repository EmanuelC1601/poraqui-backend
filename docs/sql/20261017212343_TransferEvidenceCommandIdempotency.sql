BEGIN TRANSACTION;
ALTER TABLE [TransferReviews] ADD [CommandId] uniqueidentifier NULL;

ALTER TABLE [TransferReviews] ADD [ContentHash] nvarchar(64) NULL;

ALTER TABLE [TransferReviews] ADD [SubmittedByUserId] uniqueidentifier NULL;

CREATE UNIQUE INDEX [IX_TransferReviews_SubmittedByUserId_CommandId] ON [TransferReviews] ([SubmittedByUserId], [CommandId]) WHERE [CommandId] IS NOT NULL;

ALTER TABLE [TransferReviews] ADD CONSTRAINT [FK_TransferReviews_AspNetUsers_SubmittedByUserId] FOREIGN KEY ([SubmittedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017212343_TransferEvidenceCommandIdempotency', N'10.0.13');

COMMIT;
GO

