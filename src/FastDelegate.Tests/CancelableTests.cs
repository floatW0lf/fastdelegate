using FastDelegate.Rewriter;

namespace FastDelegate.Tests;

public class CancelableTests
{
    [Test]
    public void must_be_canceled()
    {
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        Assert.Catch<OperationCanceledException>(() =>
        {
            var result = Enumerable.Range(0, 100)
                .Cancelable(source.Token)
                .Select(x =>
                {
                    Thread.Sleep(100);
                    return x;
                }).ToArray();
            
            Assert.That(result, Is.EquivalentTo(new []{0,1,2,3,4}));
        });
    }
}