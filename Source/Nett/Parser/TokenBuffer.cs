﻿namespace Nett.Parser
{
    using System;

    internal sealed class TokenBuffer : LookaheadBuffer<Token>
    {
        private bool autoThrowAwayNewlines = false;

        public TokenBuffer(Func<Token?> read, int lookAhead)
            : base(read, lookAhead)
        {
        }

        public Token Expect(TokenType tt)
        {
            var t = this.Peek();
            if (t.type != tt)
            {
                throw new Exception($"Expected token of type '{tt}' but token with value of '{t.value}' and the type '{t.type}' was found.");
            }

            return t;
        }

        public Token ExpectAndConsume(TokenType tt)
        {
            this.Expect(tt);
            return this.Consume();
        }

        public bool TryExpect(string tokenValue)
        {
            return this.Peek().value == tokenValue;
        }

        public bool TryExpect(TokenType tt)
        {
            return !this.End && this.Peek().type == tt;
        }

        public bool TryExpectAt(int index, TokenType tt)
        {
            return !this.End && this.PeekAt(index).type == tt;
        }

        public bool TryExpectAt(TokenType tt)
        {
            return this.Peek().type == tt;
        }

        public void ConsumeAllNewlines()
        {
            while (this.Peek().type == TokenType.NewLine) { this.Consume(); }
        }

        private Token ConsumeInternal()
        {
            var t = this.Consume();

            if (this.autoThrowAwayNewlines)
            {
                this.ConsumeAllNewlines();
            }

            return t;
        }

        private class AutoThrowAwayNewLinesContext : IDisposable
        {
            private readonly TokenBuffer buffer;

            public AutoThrowAwayNewLinesContext(TokenBuffer buffer)
            {
                this.buffer = buffer;
                buffer.autoThrowAwayNewlines = true;
            }

            public void Dispose() => this.buffer.autoThrowAwayNewlines = false;
        }
    }
}
