using FastDelegate.Rewriter;
using Microsoft.Build.Framework;
using Mono.Cecil;
using BuildTask = Microsoft.Build.Utilities.Task;

namespace FastDelegate;

public class LambdaRewriterTask : BuildTask, ICancelableTask
{
    private ILRewriter _rewriter = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;
    
    [Required]
    public string AssemblyFile { set; get; } = null!;
    
    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.High, "Rewrite start for {0}", AssemblyFile);
        _cancellationTokenSource = new CancellationTokenSource();
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(AssemblyFile, DefaultReaderParameters.Value);
        using (_rewriter = new ILRewriter(assemblyDefinition, new DefaultDelegateInterfaceGenerator(), new IDelegateInvokeRewriter[] { new CaptureClosureDelegateInvokeRewriter(), new NonCaptureDelegateInvokeRewriter() }))
        {
            try
            {
                _rewriter.Rewrite(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
                return false;
            }
            _rewriter.SaveChanges(AssemblyFile);
            Log.LogMessage(MessageImportance.High, "Rewrite end for {0}", AssemblyFile);
            return true;
        }
    }

    public void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }
}