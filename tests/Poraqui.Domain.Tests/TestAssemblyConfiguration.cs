using Xunit;

// API test hosts temporarily configure process-wide environment variables before the minimal host starts.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
