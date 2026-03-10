using System.Globalization;

namespace MathLearning.Infrastructure.Services.QuestionAuthoring;

internal static class MathExpressionEngine
{
    public static bool TryEvaluate(
        string expression,
        IReadOnlyDictionary<string, double> variables,
        out double value,
        out string? error)
    {
        value = default;
        error = null;

        try
        {
            var tokens = Tokenize(expression);
            var rpn = ToReversePolishNotation(tokens);
            value = EvaluateReversePolishNotation(rpn, variables);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                error = "Expression evaluates to a non-finite value.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static List<Token> Tokenize(string expression)
    {
        var tokens = new List<Token>();
        for (var index = 0; index < expression.Length;)
        {
            var current = expression[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (char.IsDigit(current) || current == '.')
            {
                var start = index;
                index++;
                while (index < expression.Length && (char.IsDigit(expression[index]) || expression[index] == '.'))
                {
                    index++;
                }

                var rawNumber = expression[start..index];
                if (!double.TryParse(rawNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    throw new InvalidOperationException($"Invalid numeric literal '{rawNumber}'.");
                }

                tokens.Add(Token.Number(number));
                continue;
            }

            if (char.IsLetter(current))
            {
                var start = index;
                index++;
                while (index < expression.Length && char.IsLetter(expression[index]))
                {
                    index++;
                }

                var identifier = expression[start..index];
                tokens.Add(IsFunction(identifier)
                    ? Token.Function(identifier)
                    : Token.Identifier(identifier));
                continue;
            }

            if (current == '(')
            {
                tokens.Add(Token.LeftParen());
                index++;
                continue;
            }

            if (current == ')')
            {
                tokens.Add(Token.RightParen());
                index++;
                continue;
            }

            if (current == ',' || current == '=')
            {
                throw new InvalidOperationException($"Unsupported token '{current}'.");
            }

            if ("+-*/^".Contains(current, StringComparison.Ordinal))
            {
                var isUnaryMinus = current == '-' &&
                                   (tokens.Count == 0 || tokens[^1].Kind is TokenKind.Operator or TokenKind.LeftParen);
                tokens.Add(isUnaryMinus ? Token.Function("neg") : Token.Operator(current.ToString()));
                index++;
                continue;
            }

            throw new InvalidOperationException($"Unsupported token '{current}'.");
        }

        return tokens;
    }

    private static Queue<Token> ToReversePolishNotation(List<Token> tokens)
    {
        var output = new Queue<Token>();
        var operators = new Stack<Token>();

        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case TokenKind.Number:
                case TokenKind.Identifier:
                    output.Enqueue(token);
                    break;
                case TokenKind.Function:
                    operators.Push(token);
                    break;
                case TokenKind.Operator:
                    while (operators.Count > 0 &&
                           ((operators.Peek().Kind == TokenKind.Function) ||
                            (operators.Peek().Kind == TokenKind.Operator &&
                             (Precedence(operators.Peek()) > Precedence(token) ||
                              (Precedence(operators.Peek()) == Precedence(token) && !IsRightAssociative(token))))))
                    {
                        output.Enqueue(operators.Pop());
                    }

                    operators.Push(token);
                    break;
                case TokenKind.LeftParen:
                    operators.Push(token);
                    break;
                case TokenKind.RightParen:
                    while (operators.Count > 0 && operators.Peek().Kind != TokenKind.LeftParen)
                    {
                        output.Enqueue(operators.Pop());
                    }

                    if (operators.Count == 0 || operators.Peek().Kind != TokenKind.LeftParen)
                    {
                        throw new InvalidOperationException("Mismatched parentheses.");
                    }

                    operators.Pop();
                    if (operators.Count > 0 && operators.Peek().Kind == TokenKind.Function)
                    {
                        output.Enqueue(operators.Pop());
                    }
                    break;
            }
        }

        while (operators.Count > 0)
        {
            var token = operators.Pop();
            if (token.Kind is TokenKind.LeftParen or TokenKind.RightParen)
            {
                throw new InvalidOperationException("Mismatched parentheses.");
            }

            output.Enqueue(token);
        }

        return output;
    }

    private static double EvaluateReversePolishNotation(Queue<Token> rpn, IReadOnlyDictionary<string, double> variables)
    {
        var stack = new Stack<double>();

        foreach (var token in rpn)
        {
            switch (token.Kind)
            {
                case TokenKind.Number:
                    stack.Push(token.NumericValue);
                    break;
                case TokenKind.Identifier:
                    if (!variables.TryGetValue(token.Text, out var variableValue))
                    {
                        throw new InvalidOperationException($"Missing value for variable '{token.Text}'.");
                    }

                    stack.Push(variableValue);
                    break;
                case TokenKind.Function:
                    if (token.Text == "neg")
                    {
                        EnsureOperandCount(stack, 1);
                        stack.Push(-stack.Pop());
                        break;
                    }

                    EnsureOperandCount(stack, 1);
                    var operand = stack.Pop();
                    stack.Push(ApplyFunction(token.Text, operand));
                    break;
                case TokenKind.Operator:
                    EnsureOperandCount(stack, 2);
                    var right = stack.Pop();
                    var left = stack.Pop();
                    stack.Push(ApplyOperator(token.Text, left, right));
                    break;
            }
        }

        if (stack.Count != 1)
        {
            throw new InvalidOperationException("Expression could not be evaluated.");
        }

        return stack.Pop();
    }

    private static void EnsureOperandCount(Stack<double> stack, int required)
    {
        if (stack.Count < required)
        {
            throw new InvalidOperationException("Expression is missing operands.");
        }
    }

    private static bool IsFunction(string identifier)
        => identifier is "sqrt" or "sin" or "cos" or "tan" or "log" or "ln";

    private static int Precedence(Token token)
        => token.Text switch
        {
            "^" => 4,
            "*" or "/" => 3,
            "+" or "-" => 2,
            _ => 5
        };

    private static bool IsRightAssociative(Token token)
        => token.Text == "^";

    private static double ApplyFunction(string name, double value)
        => name switch
        {
            "sqrt" => value < 0 ? throw new InvalidOperationException("sqrt domain error.") : Math.Sqrt(value),
            "sin" => Math.Sin(value),
            "cos" => Math.Cos(value),
            "tan" => Math.Tan(value),
            "log" => value <= 0 ? throw new InvalidOperationException("log domain error.") : Math.Log10(value),
            "ln" => value <= 0 ? throw new InvalidOperationException("ln domain error.") : Math.Log(value),
            _ => throw new InvalidOperationException($"Unsupported function '{name}'.")
        };

    private static double ApplyOperator(string op, double left, double right)
        => op switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => Math.Abs(right) < 1e-12 ? throw new InvalidOperationException("Division by zero.") : left / right,
            "^" => Math.Pow(left, right),
            _ => throw new InvalidOperationException($"Unsupported operator '{op}'.")
        };

    private enum TokenKind
    {
        Number,
        Identifier,
        Function,
        Operator,
        LeftParen,
        RightParen
    }

    private sealed record Token(TokenKind Kind, string Text, double NumericValue = default)
    {
        public static Token Number(double value) => new(TokenKind.Number, value.ToString(CultureInfo.InvariantCulture), value);
        public static Token Identifier(string text) => new(TokenKind.Identifier, text);
        public static Token Function(string text) => new(TokenKind.Function, text);
        public static Token Operator(string text) => new(TokenKind.Operator, text);
        public static Token LeftParen() => new(TokenKind.LeftParen, "(");
        public static Token RightParen() => new(TokenKind.RightParen, ")");
    }
}
