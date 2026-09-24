namespace Disl;

/// A routine with every type parameter bound: one LLVM function (or one external declaration).
public sealed class Instance(RoutineDecl decl, Compiler.TypeEnv env, string symbol, List<DType> parameters, DType ret)
{
    public RoutineDecl Decl { get; } = decl;
    public Compiler.TypeEnv Env { get; } = env;
    public string Symbol { get; } = symbol;
    public List<DType> Params { get; } = parameters;
    public DType Ret { get; } = ret;

    public bool IsExternalC => Decl.Attr("external") is { First: "c" };
    public bool IsTemplate => Decl.Attr("template") is not null;
    public bool NoReturn => Decl.Attr("noreturn") is not null;
    public bool Variadic => Decl.Attr("variadic") is not null;
    public string CallConv => Decl.Attr("callconv")?.First ?? (IsExternalC ? "c" : "disl");

    /// `fastcc ` / `coldcc ` / empty for the C convention (which ordinary Disl routines also use for now).
    public string CcPrefix => CallConv switch
    {
        "fast" => "fastcc ",
        "cold" => "coldcc ",
        _ => "",
    };

    public string FnAttrs
    {
        get
        {
            var attrs = new List<string>();
            if (NoReturn) attrs.Add("noreturn");
            if (Decl.Attr("nounwind") is not null) attrs.Add("nounwind");
            return attrs.Count == 0 ? "" : " " + string.Join(" ", attrs);
        }
    }

    public string LlvmParamTypes =>
        string.Join(", ", Params.Select(p => p.Llvm).Concat(Variadic ? ["..."] : []));
}

public sealed partial class Compiler
{
    private static readonly HashSet<string> KnownAttributes =
        ["external", "symbol", "callconv", "noreturn", "nounwind", "variadic", "template", "target", "feature", "llvm",
         "export"];

    /// Resolves a routine's signature in `env` and gives it a symbol. Does not emit anything.
    public Instance Signature(RoutineDecl r, TypeEnv env)
    {
        foreach (var a in r.Attributes)
            if (!KnownAttributes.Contains(a.Name))
                throw new CompileError(a.Pos, $"attribute '@{a.Name}' is not supported by this compiler yet");

        var needed = new HashSet<string>(OwnerTypeParams(r).Concat(r.TypeParams));
        foreach (var n in needed)
            if (!env.Has(n))
                throw new CompileError(r.Pos, $"type parameter '{n}' of '{r.DisplayName}' could not be inferred; pass it explicitly");

        var ps = new List<DType>();
        foreach (var p in r.Params)
        {
            var t = ResolveType(p.Type, env);
            CheckSigil(p.Name, p.Type, t, env, p.Pos);
            ps.Add(t);
        }
        var ret = ResolveType(r.ReturnType, env, allowVoid: true);

        var external = r.Attr("external");
        if (external is not null && external.First is not ("c" or "llvm"))
            throw new CompileError(external.Pos, "only @external(\"c\") and @external(\"llvm\") are supported");
        if (external is { First: "llvm" } && r.Attr("template") is null)
            throw new CompileError(r.Pos, $"@external(\"llvm\") routine '{r.DisplayName}' needs a @template");
        if (external is not null && r.Blocks is not null)
            throw new CompileError(r.Pos, $"external routine '{r.DisplayName}' cannot have a body");
        if (external is null && r.Blocks is null)
            throw new CompileError(r.Pos, $"routine '{r.DisplayName}' has no body (mark it @external to declare it)");

        string symbol;
        if (external is not null) symbol = r.Attr("symbol")?.First ?? r.Name;
        else if (r.Owner is null && r.Name == "main" && r.TypeParams.Count == 0)
        {
            if (ps.Count != 0 || ret is not IntType { Bits: 32 })
                throw new CompileError(r.Pos, "main must be declared 'routine main() -> I32'");
            symbol = "main";
        }
        else
        {
            string owner = r.Owner is null ? "" : (env.Get("Self") ?? env.Get(r.Owner.Name))!.Name + ".";
            string targs = r.TypeParams.Count == 0 ? "" : $"<{string.Join(", ", r.TypeParams.Select(p => env.Get(p)!.Name))}>";
            symbol = $"_D.{owner}{r.Name}{targs}";
        }

        return new Instance(r, env, symbol, ps, ret);
    }

    /// Returns the instance for a call, queueing its body for emission (or its declaration) the first time.
    public Instance RequireInstance(RoutineDecl r, TypeEnv env)
    {
        var sig = Signature(r, env);
        if (_instances.TryGetValue(sig.Symbol, out var existing))
        {
            if (existing.Decl != r && !sig.IsExternalC)
                throw new CompileError(r.Pos, $"'{sig.Symbol}' is defined more than once (also at {existing.Decl.Pos})");
            return sig.IsExternalC ? sig : existing;
        }
        _instances[sig.Symbol] = sig;

        foreach (var p in sig.Params) EnsureTypeDefined(p);
        EnsureTypeDefined(sig.Ret);

        if (sig.IsExternalC)
        {
            if (_declaredSymbols.Add(sig.Symbol))
                _declares.AppendLine($"declare {sig.CcPrefix}{sig.Ret.Llvm} @{Quote(sig.Symbol)}({sig.LlvmParamTypes}){sig.FnAttrs}");
        }
        else if (!sig.IsTemplate)
        {
            _pending.Enqueue(sig);
        }
        return sig;
    }

    private void EmitInstance(Instance inst)
    {
        new FunctionGen(this, inst, _functions).Emit();
        // `@export("name")` adds a plain C symbol for the routine.
        if (inst.Decl.Attr("export") is { } export)
        {
            string name = export.First ?? throw new CompileError(export.Pos, "@export needs a symbol name");
            _functions.AppendLine($"@{Quote(name)} = alias {inst.Ret.Llvm} ({inst.LlvmParamTypes}), ptr @{Quote(inst.Symbol)}");
            _functions.AppendLine();
        }
    }

    public static string Quote(string symbol) =>
        symbol.Length > 0 && !char.IsAsciiDigit(symbol[0])
                          && symbol.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '$')
            ? symbol
            : $"\"{symbol}\"";
}
