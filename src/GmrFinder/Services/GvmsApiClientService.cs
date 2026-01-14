using System.Diagnostics;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract;
using Defra.TradeImportsGmrFinder.GvmsClient.Contract.Requests;
using GmrFinder.Metrics;

namespace GmrFinder.Services;

public class GvmsApiClientService(IGvmsApiClient gvmsApiClient, GvmsApiMetrics metrics) : IGvmsApiClientService
{
    public async Task<HttpResponseContent<GvmsResponse>> SearchForGmrsByMrn(
        MrnSearchRequest request,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        string? errorType = null;
        HttpResponseContent<GvmsResponse>? response;

        try
        {
            response = await gvmsApiClient.SearchForGmrs(request, cancellationToken);
            success = true;
        }
        catch (Exception ex)
        {
            errorType = ex.GetType().Name;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            metrics.RecordRequestDuration("SearchForGmrs_Mrn", success, stopwatch.Elapsed, errorType);
        }

        return response;
    }
}
