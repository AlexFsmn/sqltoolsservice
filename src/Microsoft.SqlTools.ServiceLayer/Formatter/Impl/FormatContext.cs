﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Babel.ParserGenerator;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;

namespace Microsoft.SqlTools.ServiceLayer.Formatter
{
    internal class FormatContext
    {
        private ReplacementQueue replacements = new ReplacementQueue();
        private string formattedSql;

        internal FormatContext(SqlScript sqlScript, FormatOptions options)
        {
            FormatOptions = options;
            Script = sqlScript;
            LoadKeywordIdentifiers();
        }

        internal SqlScript Script { get; private set; }
        internal FormatOptions FormatOptions { get; set; }
        internal int IndentLevel { get; set; }
        internal HashSet<int> KeywordIdentifiers { get; set; }

        private void LoadKeywordIdentifiers()
        {
            KeywordIdentifiers = new HashSet<int>();
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_FROM);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_SELECT);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_TABLE);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_CREATE);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_USEDB);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_NOT);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_NULL);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_IDENTITY);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_ORDER);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_BY);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_DESC);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_ASC);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_GROUP);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_WHERE);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_JOIN);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_ON);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_UNION);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_ALL);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_EXCEPT);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_INTERSECT);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_INTO);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_DEFAULT);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_WITH);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_AS);
            KeywordIdentifiers.Add(FormatterTokens.LEX_BATCH_SEPERATOR);
            KeywordIdentifiers.Add(FormatterTokens.TOKEN_IS);
        }

        public string FormattedSql
        {
            get
            {
                if (formattedSql == null)
                {
                    DoFormatSql();
                }
                return formattedSql;
            }
        }

        private void DoFormatSql()
        {
            StringBuilder code = new StringBuilder(Script.Sql);
            foreach (Replacement r in Replacements)
            {
                r.Apply((int position, int length, string formattedText) =>
                {
                    if (length > 0)
                    {
                        if (formattedText.Length > 0)
                        {
                            code.Remove(position, length);
                            code.Insert(position, formattedText);
                        }
                        else
                        {
                            code.Remove(position, length);
                        }
                    }
                    else
                    {
                        if (formattedText.Length > 0)
                        {
                            code.Insert(position, formattedText);
                        }
                        else
                        {
                            throw new FormatException(SR.ErrorEmptyStringReplacement);
                        }
                    }
                });
            }
            formattedSql = code.ToString();
        }

        public ReplacementQueue Replacements
        {
            get
            {
                return replacements;
            }
        }

        internal void IncrementIndentLevel()
        {
            IndentLevel++;
        }

        internal void DecrementIndentLevel()
        {
            if (IndentLevel == 0)
            {
                throw new FormatFailedException("can't decrement indent level.  It is already 0.");
            }
            IndentLevel--;
        }

        public string GetIndentString()
        {
            if (FormatOptions.UseTabs)
            {
                return new string('\t', IndentLevel);
            }
            else
            {
                return new string(' ', IndentLevel * FormatOptions.SpacesPerIndent);
            }
        }

        internal string GetTokenRangeAsOriginalString(int startTokenNumber, int endTokenNumber)
        {
            string sql = string.Empty;
            if (endTokenNumber > startTokenNumber && startTokenNumber > -1 && endTokenNumber > -1)
            {
                sql = Script.TokenManager.GetText(startTokenNumber, endTokenNumber);
            }
            return sql;
        }

        /// <summary>
        /// Will apply any token-level formatting (e.g., uppercase/lowercase of keywords).
        /// </summary>
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase")]
        internal void ProcessTokenRange(int startTokenNumber, int endTokenNumber)
        {

            for (int i = startTokenNumber; i < endTokenNumber; i++)
            {
                string sql = GetTokenRangeAsOriginalString(i, i + 1);

                if (IsKeywordToken(Script.TokenManager.TokenList[i].TokenId))
                {
                    if (FormatOptions.UppercaseKeywords)
                    {
                        TokenData tok = Script.TokenManager.TokenList[i];
                        Replacements.Add(new Replacement(tok.StartIndex, sql, sql.ToUpperInvariant()));
                        sql = sql.ToUpperInvariant();
                    }
                    else if (FormatOptions.LowercaseKeywords)
                    {
                        TokenData tok = Script.TokenManager.TokenList[i];
                        Replacements.Add(new Replacement(tok.StartIndex, sql, sql.ToLowerInvariant()));
                        sql = sql.ToLowerInvariant();
                    }
                }
            }

        }

        internal void AppendTokenRangeAsString(int startTokenNumber, int endTokenNumber)
        {
            ProcessTokenRange(startTokenNumber, endTokenNumber);
        }

        private bool IsKeywordToken(int tokenId)
        {
            return KeywordIdentifiers.Contains(tokenId);
        }

        internal List<PaddedSpaceSeparatedListFormatter.ColumnSpacingFormatDefinition> CurrentColumnSpacingFormatDefinitions { get; set; }

    }

}