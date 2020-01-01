﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Pianoforte.Sena.Lang
{
  public class Lexer : IDisposable
  {
    private readonly StreamReader inputReader;
    private char head;
    private TokenPosition position;
    private bool endOfInput;

    private bool IsEol { get => !endOfInput && head == '\n'; }

    public Lexer(string filename, Stream input)
    {
      inputReader = new StreamReader(input);
      position = new TokenPosition(filename, 1, 1);
      Peek();
    }


    #region IDisposable Support
    private bool disposed = false;

    protected virtual void Dispose(bool disposing)
    {
      if (!disposed)
      {
        if (disposing)
        {
          inputReader.Dispose();
        }
        disposed = true;
      }
    }

    public void Dispose()
    {
      Dispose(true);
    }
    #endregion

    private void Peek()
    {
      var n = inputReader.Peek();
      if (n == -1)
      {
        head = '\0';
        endOfInput = true;
      }
      else
      {
        head = (char)n;
      }
    }

    private void Consume()
    {
      if (IsEol)
      {
        position.Line += 1;
        position.Column = 0;
      }
      inputReader.Read();
      Peek();
      position.Column += 1;  
    }

    private void SkipWhiteSpaces()
    {
      while (!IsEol && char.IsWhiteSpace(head))
      {
        Consume();
      }
    }

    private bool IsIdentifierLetter(char c)
      => char.IsLetter(c) || char.IsDigit(c) || c == '_';

    private bool IsIdentifierFirstLetter(char c)
      => IsIdentifierLetter(c) && !char.IsDigit(c);

    private Token ReadIdentifierOrKeyword()
    {
      var pos = position;
      var sb = new StringBuilder();
      if (!IsIdentifierFirstLetter(head))
      {
        throw new Exception(string.Format("Unexpected {0}", head));
      }
      while (IsIdentifierLetter(head))
      {
        sb.Append(head);
        Consume();
      }

      var str = sb.ToString();
      var kind = TokenKind.Identifier;
      Keyword keyword;
      if (Keywords.Map.TryGetValue(str, out keyword))
      {
        kind = keyword.TokenKind;
      }
      return new Token(kind, str, pos);
    }

    private Token ReadNumberLiteral()
    {
      var foundPoint = false;
      var sb = new StringBuilder();
      var pos = position;
      while (char.IsDigit(head) || !foundPoint && head == '.')
      {
        sb.Append(head);
        if (head == '.')
        {
          foundPoint = true;
        }
        Consume();
      }
      return new Token(TokenKind.NumberLiteral, sb.ToString(), pos);
    }

    private char ReadEscapeSequence()
    {
      if (head != '\\')
      {
        throw new Exception("Not escapesequence");
      }
      Consume();
      switch(head)
      {
        case '\\':
          return '\\';
        case '"':
          return '"';
        case 'n':
          return '\n';
        case 'r':
          return '\r';
        case 't':
          return '\t';
      }
      throw new Exception("Invalid escapesequence");
    }

    private Token ReadStringLiteral(char quote)
    {
      var pos = position;

      if (head != quote)
      {
        throw new Exception(String.Format("Unexpected {0}", head));
      }
      Consume();

      var sb = new StringBuilder();
      var escaping = false;
      while (true)
      {
        if (endOfInput || IsEol)
        {
          throw new Exception("Unterminated String Literal");
        }

        if (head == '\\')
        {
          sb.Append(ReadEscapeSequence());
        }
        else if (head == quote)
        {
          Consume();
          return new Token(TokenKind.StringLiteral, sb.ToString(), pos);
        }
        else
        {
          sb.Append(head);
        }
        Consume();
      }
    }

    private bool IsSymbol(char c)
    {
      switch (c)
      {
        case '+':
        case '-':
        case '*':
        case '/':
        case '=':
        case '!':
        case '(':
        case ')':
        case '[':
        case ']':
        case '<':
        case '>':
          return true;
      }
      return false;
    }

    private Token ReadSymbol()
    {
      var pos = position;
      var c = head;
      Consume();
      switch (c)
      {
        case '+':
          if (head == '=')
          {
            Consume();
            return new Token(TokenKind.OpPlusAssignment, "+=", pos);
          }
          return new Token(TokenKind.OpPlus, "+", pos);
        case '-':
          if (head == '=')
          {
            Consume();
            return new Token(TokenKind.OpMinusAssignment, "-=", pos);
          }
          return new Token(TokenKind.OpMinus, "-", pos);
        case '*':
          if (head == '=')
          {
            Consume();
            return new Token(TokenKind.OpMultiplicationAssignment, "*=", pos);
          }
          return new Token(TokenKind.OpMultiplication, "*", pos);
        case '/':
          if (head == '=')
          {
            Consume();
            return new Token(TokenKind.OpDivisionAssignment, "/=", pos);
          }
          return new Token(TokenKind.OpDivision, "/", pos);
        case '=':
          if (head == '=')
          {
            Consume();
            return new Token(TokenKind.OpEqual, "==", pos);
          }
          return new Token(TokenKind.OpAssignment, "=", pos);
        case '!':
          if (head == '=')
          {
            Consume();
            return new Token(TokenKind.OpNotEqual, "!=", pos);
          }
          break;
        case '<':
          if (head == '=')
          {
            Consume();
            return new Token(TokenKind.OpLessThanOrEqual, "<=", pos);
          }
          return new Token(TokenKind.OpLessThan, "<", pos);
        case '>':
          if (head == '=')
          {
            Consume();
            return new Token(TokenKind.OpGreaterThanOrEqual, ">=", pos);
          }
          return new Token(TokenKind.OpGreaterThan, ">", pos);
        case '(':
          return new Token(TokenKind.ParenLeft, "(", pos);
        case ')':
          return new Token(TokenKind.ParenRight, ")", pos);
        case '[':
          return new Token(TokenKind.SquareBracketLeft, "[", pos);
        case ']':
          return new Token(TokenKind.SquareBracketRight, "]", pos);
      }
      throw new Exception("Invalid Symbol");
    }

    public Token Next()
    {
      var sb = new StringBuilder();
      SkipWhiteSpaces();

      var pos = position;

      if (IsEol)
      {
        Consume();
        return new Token(TokenKind.EndOfLine, "\n", pos);
      }

      if (endOfInput)
      {
        return new Token(TokenKind.EndOfFile, "", pos);
      }

      SkipWhiteSpaces();
      switch (head)
      {
        case char c when char.IsDigit(c):
          return ReadNumberLiteral();
        case char c when IsIdentifierFirstLetter(c):
          return ReadIdentifierOrKeyword();
        case char c when IsSymbol(c):
          return ReadSymbol();
        case '"':
          return ReadStringLiteral('"');
        case '\'':
          return ReadStringLiteral('\'');
          
      }
      throw new Exception($"{pos}: Lexical Error '{head}'");
    }

    public IEnumerable<Token> Lex()
    {
      while (!endOfInput)
      {
        yield return Next();
      }
      yield return Next();
    }
  }
}
