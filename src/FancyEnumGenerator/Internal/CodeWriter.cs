using System.Collections;
using System.Text;

namespace FancyEnumGenerator.Internal;

internal delegate void CodeWriterFileInitializer(CodeWriter codeWriter);

internal interface IWriter<T>
{
     T NewLine(ushort count = 1);
     T Append(string value);
     T Append(char value);
     T Indent();
     T Unindent();
     T AppendLinesSplit(string str);
     T WriteComment(string comment);
     T AppendLine(string value);
     T AppendLine(ReadOnlySpan<char> value);
     T AppendSimpleIf(string condition, string body);
     CodeWriter.BracedWriter StartBraced(string? starter, bool new_line=true);
      CodeWriter.SwitchWriter StartSwitchValue(string starter);
     CodeWriter.SwitchCaseWriter StartSwitchCases(string starter, string? defaultEnder = null);
}




internal sealed class CodeWriter
{

    public sealed class BracedWriter : IWriter<BracedWriter>, IDisposable
    {
        private CodeWriter _cw;
        private bool _disposed;

        private bool _newLineAtEnd;
        public BracedWriter(CodeWriter cw, bool newLineAtEnd=true)
        {
            _cw = cw;
            _cw.OpenBrace();
            _newLineAtEnd = newLineAtEnd;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _cw.CloseBraceInternal();
            if (_newLineAtEnd)
            {
                _cw.NewLine();
            }
        }

        public BracedWriter NewLine(ushort count = 1)
        {
            _cw.NewLine(count);
            return this;
        }
        public BracedWriter Append(string value)
        {
            _cw.Append(value);
            return this;
        }
        public BracedWriter Append(char value)
        {
            _cw.Append(value);
            return this;
        }
        public BracedWriter Indent()
        {
            _cw.Indent();
            return this;
        }
        public BracedWriter Unindent()
        {
            _cw.Unindent();
            return this;
        }
        public BracedWriter OpenBrace()
        {
            _cw.OpenBrace();
            return this;
        }
        public BracedWriter CloseBrace()
        {
            _cw.CloseBrace();
            return this;
        }
        public BracedWriter AppendLinesSplit(string str)
        {
            _cw.AppendLinesSplit(str);
            return this;
        }
        public BracedWriter WriteComment(string comment)
        {
            _cw.WriteComment(comment);
            return this;
        }
        public BracedWriter AppendLine(string value)
        {
            _cw.AppendLine(value);
            return this;
        }
        public BracedWriter AppendLine(ReadOnlySpan<char> value)
        {
            _cw.AppendLine(value);
            return this;
        }
        public BracedWriter AppendSimpleIf(string condition, string body)
        {
            _cw.AppendSimpleIf(condition, body);
            return this;
        }

        public BracedWriter StartBraced(string? starter, bool new_line=true)
        {
           return _cw.StartBraced(starter, new_line);
        }
        public SwitchWriter StartSwitchValue(string starter)
        {
            return _cw.StartSwitchValue(starter);
        }
        public SwitchCaseWriter StartSwitchCases(string starter, string? defaultEnder = null)
        {
            return _cw.StartSwitchCases(starter, defaultEnder);
        }
        public IfStatementWriter StartIfStatement(string condition, Action<CodeWriter> writeBody)
        {
            return _cw.StartIfStatement(condition, writeBody);
        }
        public BracedWriter AppendIf(string condition, Action<CodeWriter> writeBody, Action<CodeWriter>? writeElseBody = null)
        {
            _cw.AppendIf(condition, writeBody, writeElseBody);
            return this;
        }
    }

    public ref struct SwitchWriter : IDisposable
    {
        private CodeWriter _cw;
        private int SwtichCountSoFar = 0;
        private bool _ended;
        internal SwitchWriter(CodeWriter cw)
        {
            _cw = cw;
            cw.OpenBrace();
        }

        public void AddBranch(string option, string value)
        {
            if (SwtichCountSoFar > 0)
            {
                _cw.Append(',');

            }
            _cw.AppendLine($"{option} => {value}");
            SwtichCountSoFar++;
        }
        public void AddTypeCastBranch(string option, string as_name, string value)
        {
            if (SwtichCountSoFar > 0)
            {
                _cw.Append(',');

            }
            _cw.AppendLine($"{option} {as_name} => {value}");
            SwtichCountSoFar++;
        }


        public void AddBrachWhen(string option, string condition, string value)
        {
            if (SwtichCountSoFar > 0)
            {
                _cw.Append(',');

            }
            _cw.AppendLine($"{option} when ({condition}) => {value}");
            SwtichCountSoFar++;
        }

        public CodeWriter AddDefaultArm(string value, bool addSemiColon = true)
        {
            if (SwtichCountSoFar > 0)
            {
                _cw.Append(',');

            }
            _cw.AppendLine($"_ => {value}");
            _cw.CloseBraceInternal();
            _cw.Append(";");
            _cw.NewLine();
            _ended = true;
            return _cw;
        }

        public void Dispose()
        {
            if (!_ended)
            {
                _cw.CloseBraceInternal();
                _cw.Append(";");
                _cw.NewLine();
                _ended = true;
            }
        }
    }

    public ref struct SwitchCaseWriter : IDisposable
    {
        private readonly CodeWriter _cw;
        private readonly string? _defaultEnder;
        private bool _ended;

        internal SwitchCaseWriter(CodeWriter cw, string? defaultEnder)
        {
            _cw = cw;
            _defaultEnder = defaultEnder;
            cw.OpenBrace();
        }

        public void AddCase(string option, Action<CodeWriter> writeBody, string? ender = null)
        {
            AddCaseLabel($"case {option}:", writeBody, ender);
        }

        public void AddDefaultCase(Action<CodeWriter> writeBody, string? ender = null)
        {
            AddCaseLabel("default:", writeBody, ender);
        }

        private void AddCaseLabel(string label, Action<CodeWriter> writeBody, string? ender)
        {
            if (_ended)
            {
                throw new InvalidOperationException("Cannot add a case after the switch statement has ended.");
            }

            var effectiveEnder = ender ?? _defaultEnder;
            if (effectiveEnder is null)
            {
                throw new InvalidOperationException("Switch case requires an explicit ender when no default ender is configured.");
            }

            _cw.AppendLine(label).Indent();
            try
            {
                writeBody(_cw);
                if (effectiveEnder.Length > 0)
                {
                    _cw.AppendLine(effectiveEnder);
                }
            }
            finally
            {
                _cw.Unindent();
            }
        }

        public void Dispose()
        {
            if (!_ended)
            {
                _cw.CloseBrace();
                _ended = true;
            }
        }
    }

    public ref struct IfStatementWriter : IDisposable
    {
        private readonly CodeWriter _cw;
        private bool _ended;

        internal IfStatementWriter(CodeWriter cw, string condition, Action<CodeWriter> writeBody)
        {
            _cw = cw;
            _ended = false;
            cw.AppendLine($"if({condition})");
            cw.AppendBracedBody(writeBody);
        }

        public void AddElseIf(string condition, Action<CodeWriter> writeBody)
        {
            if (_ended)
            {
                throw new InvalidOperationException("Cannot add an else-if clause after the if statement has ended.");
            }

            _cw.AppendLine($"else if({condition})");
            _cw.AppendBracedBody(writeBody);
        }

        public void AddElse(Action<CodeWriter> writeBody)
        {
            if (_ended)
            {
                throw new InvalidOperationException("Cannot add an else clause after the if statement has ended.");
            }

            _cw.AppendLine("else");
            _cw.AppendBracedBody(writeBody);
            _ended = true;
        }

        public void Dispose()
        {
            _ended = true;
        }
    }


    private StringBuilder sb = new StringBuilder();
    private int indent = 0;
    private static string IndentString(int ind) => ind==0?String.Empty: new String('\t', ind);

    private bool isFreshLine = true;
    private readonly bool isFile;
    private bool addedNullableDisable = false;

    public CodeWriter()
    {
    }

    public CodeWriter(CodeWriterFileInitializer newFileDefaults)
    {
        if (newFileDefaults is null)
        {
            throw new ArgumentNullException(nameof(newFileDefaults));
        }

        isFile = true;
        newFileDefaults(this);
    }

    public BracedWriter StartBraced(string? starter, bool new_line=true)
    {
        if (starter != null)
        {
            AppendLine(starter);
            if(!starter.EndsWith(" "))
            {
                Append(" ");
            }
        }
        return new BracedWriter(this, new_line);
    }



    public SwitchWriter StartSwitchValue(string starter)
    {
        AppendLine(starter);
        return new SwitchWriter(this);
    }

    public SwitchCaseWriter StartSwitchCases(string starter, string? defaultEnder = null)
    {
        AppendLine(starter);
        return new SwitchCaseWriter(this, defaultEnder);
    }

    public IfStatementWriter StartIfStatement(string condition, Action<CodeWriter> writeBody)
    {
        return new IfStatementWriter(this, condition, writeBody);
    }

    public CodeWriter AppendIf(string condition, Action<CodeWriter> writeBody, Action<CodeWriter>? writeElseBody = null)
    {
        using var statement = StartIfStatement(condition, writeBody);
        if (writeElseBody is not null)
        {
            statement.AddElse(writeElseBody);
        }

        return this;
    }



    public CodeWriter NewLine(ushort count=1)
    {
        for (var i = 0; i < count; i++)
        {
            sb.AppendLine();
        }
        sb.Append(IndentString(indent));
        isFreshLine = true;
        return this;
    }

    public CodeWriter Append(string value)
    {
        sb.Append(value);
        isFreshLine = false;
        return this;
    }
    public CodeWriter Append(char value)
    {
        sb.Append(value);
        isFreshLine = false;
        return this;
    }

    public CodeWriter AppendLine(string value)
    {
        if (!isFreshLine)
        {
            NewLine();
        }
        sb.Append(value);
        isFreshLine = false;
        return this;
    }

    public CodeWriter AppendSimpleIf(string condition, string body)
    {
        return AppendIf(condition, writer => writer.Append(body));
    }

    public CodeWriter AppendLine(ReadOnlySpan<char> value)
    {
        if (!isFreshLine)
        {
            NewLine();
        }
        sb.Append(value);
        isFreshLine = false;
        return this;
    }

    public CodeWriter Indent()
    {
        indent++;
        return NewLine();
    }

    public CodeWriter Unindent()
    {
        if (indent == 0)
        {
            return NewLine();
        }
        indent--;
        return NewLine();
    }

    public CodeWriter AppendLinesSplit(string str)
    {
        var sp = str.AsSpan();
        foreach (var s in sp.EnumerateLines())
        {
           AppendLine(s.Trim());
        }

        return this;
    }

    public CodeWriter OpenBrace()
    {
        sb.Append('{');
        return Indent();
    }

    private void AppendBracedBody(Action<CodeWriter> writeBody)
    {
        OpenBrace();
        try
        {
            writeBody(this);
        }
        finally
        {
            CloseBrace();
        }
    }

    public CodeWriter CloseBrace()
    {
        CloseBraceInternal();
        return NewLine();
    }

    private void CloseBraceInternal()
    {
        Unindent();
        sb.Append('}');
    }

    public CodeWriter WriteComment(string comment)
    {
        if (!isFreshLine)
        {
            NewLine();
        }
        sb.Append("// ").Append(comment);
        NewLine();
        return this;
    }

    public string AsString()
    {
        if (isFile && !addedNullableDisable)
        {
            addedNullableDisable = true;
            AppendLine("#nullable disable");
        }

        return sb.ToString();

    }




}
