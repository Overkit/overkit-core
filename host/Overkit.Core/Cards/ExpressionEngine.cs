using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Overkit.Host.Cards;

public sealed class ExpressionException(string message) : Exception(message);

/// <summary>
/// Interpréteur d'expressions des Cards (§5.1, EXG-040) : accès par chemin,
/// comparaisons, opérateurs logiques, filtres et agrégations.
///
/// Volontairement borné et non Turing-complet : aucune boucle, aucune
/// définition de fonction, aucun IO, aucune allocation non bornée. Chaque
/// évaluation dispose d'un budget de temps et d'un plafond d'éléments
/// parcourus ; au-delà, elle échoue proprement et la Card est suspendue avec
/// un message.
///
/// Grammaire :
///   expr     := or
///   or       := and ('or' and)*
///   and      := compare ('and' compare)*
///   compare  := pipeline (('='|'!='|'&lt;'|'&lt;='|'&gt;'|'&gt;=') pipeline)?
///   pipeline := unary ('|' call)*
///   unary    := ('not')? primary
///   primary  := number | string | true | false | path | call | '(' expr ')'
///   call     := ident '(' (expr (',' expr)*)? ')'
/// </summary>
public static class ExpressionEngine
{
    /// <summary>
    /// Plafonds d'un rendu complet de Card (toutes sections confondues). Le
    /// rendu est déjà limité à 2 Hz par l'UI : ces bornes protègent contre une
    /// expression pathologique, pas contre un usage normal.
    /// </summary>
    public const int MaxItemsScanned = 200_000;

    public static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(50);

    // Les expressions sont analysées une seule fois : sans ce cache, une liste
    // de N lignes × C colonnes relance N×C analyses syntaxiques par rendu.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Node> AstCache = new();

    public static object? Evaluate(string expression, object? root, EvaluationContext? context = null)
    {
        var ctx = context ?? new EvaluationContext();
        var node = AstCache.GetOrAdd(expression, static text =>
        {
            var parser = new Parser(Tokenizer.Tokenize(text));
            var parsed = parser.ParseExpression();
            parser.ExpectEnd();
            return parsed;
        });
        return node.Evaluate(new Scope(root, root, ctx));
    }

    /// <summary>Budget partagé par toutes les expressions d'un même rendu de Card.</summary>
    public sealed class EvaluationContext
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private int _scanned;

        public void CountScan(int items = 1)
        {
            _scanned += items;
            if (_scanned > MaxItemsScanned)
            {
                throw new ExpressionException(
                    $"trop de données parcourues ({MaxItemsScanned:N0} éléments) — restreins la source avec un filtre " +
                    "(where) ou une limite");
            }
            // Le temps ne se vérifie pas à chaque élément : l'horloge coûte
            // plus cher que le travail utile sur de petites collections.
            if ((_scanned & 0x3FF) == 0 && _stopwatch.Elapsed > Budget)
            {
                throw new ExpressionException(
                    $"évaluation trop longue (plus de {Budget.TotalMilliseconds:N0} ms) — simplifie les expressions " +
                    "ou réduis la taille des listes");
            }
        }
    }

    private readonly record struct Scope(object? Root, object? Current, EvaluationContext Context)
    {
        public Scope WithCurrent(object? item) => this with { Current = item };
    }

    // ---------- Lexer ----------

    private enum TokenKind { Number, String, Ident, Operator, LParen, RParen, Comma, Pipe, End }

    private readonly record struct Token(TokenKind Kind, string Text);

    private static class Tokenizer
    {
        private static readonly string[] TwoCharOperators = ["!=", "<=", ">="];

        public static List<Token> Tokenize(string input)
        {
            var tokens = new List<Token>();
            var i = 0;
            while (i < input.Length)
            {
                var c = input[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }
                if (char.IsDigit(c) || (c == '-' && i + 1 < input.Length && char.IsDigit(input[i + 1])))
                {
                    var start = i++;
                    while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.'))
                    {
                        i++;
                    }
                    tokens.Add(new Token(TokenKind.Number, input[start..i]));
                    continue;
                }
                if (c is '"' or '\'')
                {
                    var quote = c;
                    var start = ++i;
                    while (i < input.Length && input[i] != quote)
                    {
                        i++;
                    }
                    if (i >= input.Length)
                    {
                        throw new ExpressionException("chaîne non terminée");
                    }
                    tokens.Add(new Token(TokenKind.String, input[start..i]));
                    i++;
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    var start = i;
                    while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] is '_' or '.'))
                    {
                        i++;
                    }
                    tokens.Add(new Token(TokenKind.Ident, input[start..i]));
                    continue;
                }
                if (c == '(') { tokens.Add(new Token(TokenKind.LParen, "(")); i++; continue; }
                if (c == ')') { tokens.Add(new Token(TokenKind.RParen, ")")); i++; continue; }
                if (c == ',') { tokens.Add(new Token(TokenKind.Comma, ",")); i++; continue; }
                if (c == '|') { tokens.Add(new Token(TokenKind.Pipe, "|")); i++; continue; }

                var two = i + 1 < input.Length ? input.Substring(i, 2) : "";
                if (TwoCharOperators.Contains(two))
                {
                    tokens.Add(new Token(TokenKind.Operator, two));
                    i += 2;
                    continue;
                }
                if (c is '=' or '<' or '>')
                {
                    tokens.Add(new Token(TokenKind.Operator, c.ToString()));
                    i++;
                    continue;
                }
                throw new ExpressionException($"caractère inattendu : « {c} »");
            }
            tokens.Add(new Token(TokenKind.End, ""));
            return tokens;
        }
    }

    // ---------- AST ----------

    private abstract record Node
    {
        public abstract object? Evaluate(Scope scope);
    }

    private sealed record LiteralNode(object? Value) : Node
    {
        public override object? Evaluate(Scope scope) => Value;
    }

    private sealed record PathNode(string Path) : Node
    {
        public override object? Evaluate(Scope scope) => Resolve(scope, Path);
    }

    private sealed record NotNode(Node Inner) : Node
    {
        public override object Evaluate(Scope scope) => !Truthy(Inner.Evaluate(scope));
    }

    private sealed record BinaryNode(string Operator, Node Left, Node Right) : Node
    {
        public override object? Evaluate(Scope scope)
        {
            if (Operator is "and" or "or")
            {
                var left = Truthy(Left.Evaluate(scope));
                return Operator == "and"
                    ? left && Truthy(Right.Evaluate(scope))
                    : left || Truthy(Right.Evaluate(scope));
            }

            var a = Left.Evaluate(scope);
            var b = Right.Evaluate(scope);
            return Operator switch
            {
                "=" => AreEqual(a, b),
                "!=" => !AreEqual(a, b),
                "<" => Compare(a, b) < 0,
                "<=" => Compare(a, b) <= 0,
                ">" => Compare(a, b) > 0,
                ">=" => Compare(a, b) >= 0,
                _ => throw new ExpressionException($"opérateur inconnu : {Operator}"),
            };
        }
    }

    private sealed record CallNode(string Name, IReadOnlyList<Node> Arguments, Node? Piped) : Node
    {
        public override object? Evaluate(Scope scope)
        {
            var name = Name.ToLowerInvariant();

            // Filtres : le premier argument est la collection (ou la valeur pipée).
            if (name is "where" or "count" or "sum" or "min" or "max" or "avg" or "first" or "any")
            {
                var sourceNode = Piped ?? (Arguments.Count > 0 ? Arguments[0] : null);
                if (sourceNode is null)
                {
                    throw new ExpressionException($"{name}() attend une collection");
                }
                var source = Piped is not null ? Piped.Evaluate(scope) : Arguments[0].Evaluate(scope);
                var predicateOrSelector = Piped is not null
                    ? (Arguments.Count > 0 ? Arguments[0] : null)
                    : (Arguments.Count > 1 ? Arguments[1] : null);
                return ApplyCollectionFunction(name, source, predicateOrSelector, scope);
            }

            var args = Arguments.Select(a => a.Evaluate(scope)).ToList();
            if (Piped is not null)
            {
                args.Insert(0, Piped.Evaluate(scope));
            }

            return name switch
            {
                "round" => args.Count > 1
                    ? Math.Round(ToNumber(args[0]), (int)ToNumber(args[1]))
                    : Math.Round(ToNumber(args[0])),
                "floor" => Math.Floor(ToNumber(args[0])),
                "abs" => Math.Abs(ToNumber(args[0])),
                "percent" => args.Count > 1 && ToNumber(args[1]) != 0
                    ? ToNumber(args[0]) / ToNumber(args[1]) * 100
                    : 0d,
                // pad(5, 2) => « 05 » : pour composer une heure « 16:05 ».
                "pad" => AsString(args[0]).PadLeft(args.Count > 1 ? (int)ToNumber(args[1]) : 2, '0'),
                "lower" => AsString(args[0]).ToLowerInvariant(),
                "contains" => AsString(args[0]).Contains(AsString(args[1]), StringComparison.OrdinalIgnoreCase),
                "concat" => string.Concat(args.Select(AsString)),
                "if" => Truthy(args[0]) ? args[1] : args.Count > 2 ? args[2] : null,
                "isset" => args[0] is not null,
                _ => throw new ExpressionException($"fonction inconnue : {Name}()"),
            };
        }

        private static object? ApplyCollectionFunction(string name, object? source, Node? inner, Scope scope)
        {
            var items = AsEnumerable(source, scope.Context);

            if (name == "where")
            {
                if (inner is null)
                {
                    throw new ExpressionException("where() attend une condition");
                }
                var kept = new List<object?>();
                foreach (var item in items)
                {
                    scope.Context.CountScan();
                    if (Truthy(inner.Evaluate(scope.WithCurrent(item))))
                    {
                        kept.Add(item);
                    }
                }
                return kept;
            }

            if (name == "count")
            {
                var total = 0;
                foreach (var item in items)
                {
                    scope.Context.CountScan();
                    if (inner is null || Truthy(inner.Evaluate(scope.WithCurrent(item))))
                    {
                        total++;
                    }
                }
                return (double)total;
            }

            if (name == "any")
            {
                foreach (var item in items)
                {
                    scope.Context.CountScan();
                    if (inner is null || Truthy(inner.Evaluate(scope.WithCurrent(item))))
                    {
                        return true;
                    }
                }
                return false;
            }

            if (name == "first")
            {
                foreach (var item in items)
                {
                    scope.Context.CountScan();
                    if (inner is null || Truthy(inner.Evaluate(scope.WithCurrent(item))))
                    {
                        return item;
                    }
                }
                return null;
            }

            // sum / min / max / avg : `inner` sélectionne la valeur numérique.
            double? aggregate = null;
            var seen = 0;
            foreach (var item in items)
            {
                scope.Context.CountScan();
                var value = inner is null ? ToNumber(item) : ToNumber(inner.Evaluate(scope.WithCurrent(item)));
                seen++;
                aggregate = aggregate is null
                    ? value
                    : name switch
                    {
                        "sum" or "avg" => aggregate + value,
                        "min" => Math.Min(aggregate.Value, value),
                        "max" => Math.Max(aggregate.Value, value),
                        _ => aggregate,
                    };
            }
            if (aggregate is null)
            {
                return 0d;
            }
            return name == "avg" ? aggregate / Math.Max(1, seen) : aggregate;
        }
    }

    // ---------- Parser ----------

    private sealed class Parser(List<Token> tokens)
    {
        private int _index;

        private Token Current => tokens[_index];

        public void ExpectEnd()
        {
            if (Current.Kind != TokenKind.End)
            {
                throw new ExpressionException($"texte inattendu après l'expression : « {Current.Text} »");
            }
        }

        public Node ParseExpression() => ParseOr();

        private Node ParseOr()
        {
            var left = ParseAnd();
            while (Current is { Kind: TokenKind.Ident, Text: "or" })
            {
                _index++;
                left = new BinaryNode("or", left, ParseAnd());
            }
            return left;
        }

        private Node ParseAnd()
        {
            var left = ParseCompare();
            while (Current is { Kind: TokenKind.Ident, Text: "and" })
            {
                _index++;
                left = new BinaryNode("and", left, ParseCompare());
            }
            return left;
        }

        private Node ParseCompare()
        {
            var left = ParsePipeline();
            if (Current.Kind == TokenKind.Operator)
            {
                var op = Current.Text;
                _index++;
                return new BinaryNode(op, left, ParsePipeline());
            }
            return left;
        }

        private Node ParsePipeline()
        {
            var left = ParseUnary();
            while (Current.Kind == TokenKind.Pipe)
            {
                _index++;
                if (Current.Kind != TokenKind.Ident)
                {
                    throw new ExpressionException("après « | », un filtre est attendu (where, count…)");
                }
                var name = Current.Text;
                _index++;
                var args = ParseArgumentsIfAny();
                left = new CallNode(name, args, left);
            }
            return left;
        }

        private Node ParseUnary()
        {
            if (Current is { Kind: TokenKind.Ident, Text: "not" })
            {
                _index++;
                return new NotNode(ParseUnary());
            }
            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            var token = Current;
            switch (token.Kind)
            {
                case TokenKind.Number:
                    _index++;
                    return new LiteralNode(double.Parse(token.Text, CultureInfo.InvariantCulture));

                case TokenKind.String:
                    _index++;
                    return new LiteralNode(token.Text);

                case TokenKind.LParen:
                    _index++;
                    var inner = ParseExpression();
                    Expect(TokenKind.RParen, ")");
                    return inner;

                case TokenKind.Ident:
                    _index++;
                    if (token.Text is "true" or "false")
                    {
                        return new LiteralNode(token.Text == "true");
                    }
                    if (token.Text == "null")
                    {
                        return new LiteralNode(null);
                    }
                    if (Current.Kind == TokenKind.LParen)
                    {
                        return new CallNode(token.Text, ParseArgumentsIfAny(), null);
                    }
                    return new PathNode(token.Text);

                default:
                    throw new ExpressionException($"expression inattendue : « {token.Text} »");
            }
        }

        private List<Node> ParseArgumentsIfAny()
        {
            var args = new List<Node>();
            if (Current.Kind != TokenKind.LParen)
            {
                return args;
            }
            _index++;
            if (Current.Kind == TokenKind.RParen)
            {
                _index++;
                return args;
            }
            args.Add(ParseExpression());
            while (Current.Kind == TokenKind.Comma)
            {
                _index++;
                args.Add(ParseExpression());
            }
            Expect(TokenKind.RParen, ")");
            return args;
        }

        private void Expect(TokenKind kind, string text)
        {
            if (Current.Kind != kind)
            {
                throw new ExpressionException($"« {text} » attendu");
            }
            _index++;
        }
    }

    // ---------- Résolution de chemins ----------

    /// <summary>
    /// Résout « palbox.pals » ou « talents.hp » depuis la racine (snapshot) ou
    /// l'élément courant d'un filtre. La correspondance ignore la casse et les
    /// underscores, pour que « species_id » du State Bus se lise aussi
    /// « speciesId ».
    /// </summary>
    private static object? Resolve(Scope scope, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        // Le chemin s'applique d'abord à l'élément courant (dans un filtre),
        // puis à la racine — ce qui rend `where(level > 40)` naturel.
        var value = TryWalk(scope.Current, segments, scope.Context);
        if (value.Found)
        {
            return value.Value;
        }
        var fromRoot = TryWalk(scope.Root, segments, scope.Context);
        return fromRoot.Found ? fromRoot.Value : null;
    }

    private static (bool Found, object? Value) TryWalk(object? start, string[] segments, EvaluationContext context)
    {
        var current = start;
        foreach (var segment in segments)
        {
            context.CountScan();
            if (current is null)
            {
                return (false, null);
            }
            var property = FindProperty(current.GetType(), segment);
            if (property is null)
            {
                return (false, null);
            }
            current = property.GetValue(current);
        }
        return (true, current);
    }

    private static readonly Dictionary<(Type, string), PropertyInfo?> PropertyCache = [];

    private static PropertyInfo? FindProperty(Type type, string name)
    {
        lock (PropertyCache)
        {
            if (PropertyCache.TryGetValue((type, name), out var cached))
            {
                return cached;
            }
            var normalized = Normalize(name);
            var property = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => Normalize(p.Name) == normalized && p.GetIndexParameters().Length == 0);
            PropertyCache[(type, name)] = property;
            return property;
        }
    }

    private static string Normalize(string value) => value.Replace("_", "").ToLowerInvariant();

    // ---------- Conversions ----------

    private static IEnumerable<object?> AsEnumerable(object? value, EvaluationContext context)
    {
        switch (value)
        {
            case null:
                yield break;
            case string:
                yield return value;
                yield break;
            case IEnumerable enumerable:
                foreach (var item in enumerable)
                {
                    context.CountScan();
                    yield return item;
                }
                yield break;
            default:
                yield return value;
                yield break;
        }
    }

    public static bool Truthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        double d => d != 0,
        string s => s.Length > 0,
        ICollection collection => collection.Count > 0,
        _ => true,
    };

    public static double ToNumber(object? value) => value switch
    {
        null => 0,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        bool b => b ? 1 : 0,
        string s => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
        ICollection collection => collection.Count,
        _ => Convert.ToDouble(value, CultureInfo.InvariantCulture),
    };

    public static string AsString(object? value) => value switch
    {
        null => "",
        double d => d == Math.Floor(d) && Math.Abs(d) < 1e15
            ? ((long)d).ToString(CultureInfo.CurrentCulture)
            : d.ToString("0.##", CultureInfo.CurrentCulture),
        bool b => b ? "oui" : "non",
        string s => s,
        _ => value.ToString() ?? "",
    };

    private static bool AreEqual(object? a, object? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }
        if (a is string || b is string)
        {
            return string.Equals(AsString(a), AsString(b), StringComparison.OrdinalIgnoreCase);
        }
        if (a is bool || b is bool)
        {
            return Truthy(a) == Truthy(b);
        }
        return Math.Abs(ToNumber(a) - ToNumber(b)) < 1e-9;
    }

    private static int Compare(object? a, object? b)
    {
        if (a is string sa && b is string sb)
        {
            return string.Compare(sa, sb, StringComparison.CurrentCultureIgnoreCase);
        }
        return ToNumber(a).CompareTo(ToNumber(b));
    }
}
