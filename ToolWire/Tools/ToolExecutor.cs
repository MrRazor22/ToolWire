using ToolWire.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace ToolWire.Tools
{
    public interface IToolExecutor
    {
        IToolRegistry Registry { get; }
        event Action<ToolCall>? OnInvoking;
        event Action<ToolCall, Exception>? OnError;
        event Action<ToolCall, ToolResult>? OnCompleted;
        Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken ct = default);
    }

    public sealed class ToolExecutor : IToolExecutor
    {
        private readonly IToolRegistry _registry;
        private readonly TimeSpan? _timeout;

        public IToolRegistry Registry => _registry;

        public event Action<ToolCall>? OnInvoking;
        public event Action<ToolCall, Exception>? OnError;
        public event Action<ToolCall, ToolResult>? OnCompleted;

        public ToolExecutor(TimeSpan? timeout = null)
        {
            _registry = new ToolRegistry();
            _timeout = timeout;
        }

        public ToolExecutor(IToolRegistry registry, TimeSpan? timeout = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _timeout = timeout;
        }

        /// <summary>
        /// Executes registered tools safely for LLM use.
        /// </summary>
        public async Task<ToolResult> ExecuteAsync(
            ToolCall call,
            CancellationToken ct = default)
        {
            if (call == null) throw new ArgumentNullException(nameof(call));

            ct.ThrowIfCancellationRequested();

            var tool = _registry.TryGet(call.Name);
            if (tool == null)
            {
                var tex = new ToolException(
                    call.Name,
                    $"Tool '{call.Name}' is not registered.");

                OnError?.Invoke(call, tex);

                return new ToolResult(
                    call.Id,
                    tex.ForLlm(),
                    isError: true);
            }

            OnInvoking?.Invoke(call);

            ToolResult result;

            try
            {
                object? raw;

                if (_timeout.HasValue)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(_timeout.Value);

                    raw = await InvokeAsync(
                        tool.Executor,
                        call,
                        cts.Token).ConfigureAwait(false);
                }
                else
                {
                    raw = await InvokeAsync(
                        tool.Executor,
                        call,
                        ct).ConfigureAwait(false);
                }

                result = new ToolResult(call.Id, raw.AsJsonString());
            }
            catch (OperationCanceledException) when (_timeout.HasValue && !ct.IsCancellationRequested)
            {
                var tex = new ToolException(
                    call.Name,
                    $"Tool execution timed out after {_timeout.Value.TotalSeconds:0.##}s");

                OnError?.Invoke(call, tex);

                result = new ToolResult(
                    call.Id,
                    tex.ForLlm(),
                    isError: true);
            }
            catch (Exception ex)
            {
                var tex = NormalizeException(call, ex);

                OnError?.Invoke(call, tex);

                result = new ToolResult(
                    call.Id,
                    tex.ForLlm(),
                    isError: true);
            }

            OnCompleted?.Invoke(call, result);
            return result;
        }

        #region internals

        private static async Task<object?> InvokeAsync(
            Delegate executor,
            ToolCall call,
            CancellationToken ct)
        {
            var args = BindArguments(executor.Method, call, ct);

            try
            {
                var result = executor.DynamicInvoke(args);
                return await UnwrapAsyncResult(result).ConfigureAwait(false);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw tie.InnerException;
            }
        }

        private static async Task<object?> UnwrapAsyncResult(object? result)
        {
            if (result is Task task)
            {
                await task.ConfigureAwait(false);

                var type = task.GetType();
                if (type.IsGenericType)
                    return type.GetProperty("Result")?.GetValue(task);

                return null;
            }

            if (result is ValueTask vt)
            {
                await vt.ConfigureAwait(false);
                return null;
            }

            var resultType = result?.GetType();
            if (resultType != null &&
                resultType.IsGenericType &&
                resultType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                var asTask = resultType.GetMethod("AsTask")!;
                var vtTask = (Task)asTask.Invoke(result, null)!;
                await vtTask.ConfigureAwait(false);
                return vtTask.GetType().GetProperty("Result")?.GetValue(vtTask);
            }

            return result;
        }

        private static object?[] BindArguments(
            MethodInfo method,
            ToolCall call,
            CancellationToken ct)
        {
            var parameters = method.GetParameters();
            var final = new object?[parameters.Length];

            var argsObj = call.Arguments
                ?? throw new ToolValidationException(
                    call.Name,
                    "arguments",
                    "Arguments missing.");

            argsObj = (JObject)argsObj.DeepClone();

            if (parameters.Length == 1 &&
                parameters[0].ParameterType != typeof(CancellationToken) &&
                !parameters[0].ParameterType.IsSimpleType() &&
                !argsObj.ContainsKey(parameters[0].Name!))
            {
                argsObj = new JObject
                {
                    [parameters[0].Name!] = argsObj
                };
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];

                if (p.ParameterType == typeof(CancellationToken))
                {
                    final[i] = ct;
                    continue;
                }

                var name = p.Name!;
                var token = argsObj[name];

                if (token == null)
                {
                    if (p.HasDefaultValue)
                    {
                        final[i] = p.DefaultValue;
                        continue;
                    }

                    throw new ToolValidationException(
                        call.Name,
                        name,
                        "Missing required parameter.");
                }

                var schema = p.ParameterType.GetSchemaForType();
                var errors = schema.Validate(token, name);

                if (errors.Any())
                    throw new ToolValidationAggregateException(
                        call.Name,
                        errors);

                try
                {
                    final[i] = token.ToObject(p.ParameterType);
                }
                catch (Exception ex)
                {
                    throw new ToolValidationException(
                        call.Name,
                        name,
                        ex.Message);
                }
            }

            return final;
        }

        private static ToolException NormalizeException(
            ToolCall call,
            Exception ex)
        {
            return ex as ToolException
                ?? new ToolException(call.Name, ex.Message, ex);
        }

        #endregion
    }
}
