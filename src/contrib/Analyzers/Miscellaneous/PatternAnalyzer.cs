/*
 *
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using Version = Lucene.Net.Util.Version;

namespace Lucene.Net.Analyzers.Miscellaneous
{
    /**
     * Efficient Lucene analyzer/tokenizer that preferably operates on a String rather than a
     * {@link java.io.Reader}, that can flexibly separate text into terms via a regular expression {@link Regex}
     * (with behaviour identical to {@link String#split(String)}),
     * and that combines the functionality of
     * {@link org.apache.lucene.analysis.LetterTokenizer},
     * {@link org.apache.lucene.analysis.LowerCaseTokenizer},
     * {@link org.apache.lucene.analysis.WhitespaceTokenizer},
     * {@link org.apache.lucene.analysis.StopFilter} into a single efficient
     * multi-purpose class.
     * <p>
     * If you are unsure how exactly a regular expression should look like, consider 
     * prototyping by simply trying various expressions on some test texts via
     * {@link String#split(String)}. Once you are satisfied, give that regex to 
     * RegexAnalyzer. Also see <a target="_blank" 
     * href="http://java.sun.com/docs/books/tutorial/extra/regex/">Java Regular Expression Tutorial</a>.
     * <p>
     * This class can be considerably faster than the "normal" Lucene tokenizers. 
     * It can also serve as a building block in a compound Lucene
     * {@link org.apache.lucene.analysis.TokenFilter} chain. For example as in this 
     * stemming example:
     * <pre>
     * RegexAnalyzer pat = ...
     * TokenStream tokenStream = new SnowballFilter(
     *     pat.tokenStream("content", "James is running round in the woods"), 
     *     "English"));
     * </pre>
     *
     */
    public class PatternAnalyzer : Analyzer
    {

        /** <code>"\\W+"</code>; Divides text at non-letters (NOT char.IsLetter(c)) */
        public static readonly Regex NON_WORD_PATTERN = new Regex("\\W+", RegexOptions.Compiled);

        /** <code>"\\s+"</code>; Divides text at whitespaces (char.IsWhitespace(c)) */
        public static readonly Regex WHITESPACE_PATTERN = new Regex("\\s+", RegexOptions.Compiled);

        private static readonly CharArraySet EXTENDED_ENGLISH_STOP_WORDS =
          CharArraySet.UnmodifiableSet(new CharArraySet(new[]{
      "a", "about", "above", "across", "adj", "after", "afterwards",
      "again", "against", "albeit", "all", "almost", "alone", "along",
      "already", "also", "although", "always", "among", "amongst", "an",
      "and", "another", "any", "anyhow", "anyone", "anything",
      "anywhere", "are", "around", "as", "at", "be", "became", "because",
      "become", "becomes", "becoming", "been", "before", "beforehand",
      "behind", "being", "below", "beside", "besides", "between",
      "beyond", "both", "but", "by", "can", "cannot", "co", "could",
      "down", "during", "each", "eg", "either", "else", "elsewhere",
      "enough", "etc", "even", "ever", "every", "everyone", "everything",
      "everywhere", "except", "few", "first", "for", "former",
      "formerly", "from", "further", "had", "has", "have", "he", "hence",
      "her", "here", "hereafter", "hereby", "herein", "hereupon", "hers",
      "herself", "him", "himself", "his", "how", "however", "i", "ie", "if",
      "in", "inc", "indeed", "into", "is", "it", "its", "itself", "last",
      "latter", "latterly", "least", "less", "ltd", "many", "may", "me",
      "meanwhile", "might", "more", "moreover", "most", "mostly", "much",
      "must", "my", "myself", "namely", "neither", "never",
      "nevertheless", "next", "no", "nobody", "none", "noone", "nor",
      "not", "nothing", "now", "nowhere", "of", "off", "often", "on",
      "once one", "only", "onto", "or", "other", "others", "otherwise",
      "our", "ours", "ourselves", "out", "over", "own", "per", "perhaps",
      "rather", "s", "same", "seem", "seemed", "seeming", "seems",
      "several", "she", "should", "since", "so", "some", "somehow",
      "someone", "something", "sometime", "sometimes", "somewhere",
      "still", "such", "t", "than", "that", "the", "their", "them",
      "themselves", "then", "thence", "there", "thereafter", "thereby",
      "therefor", "therein", "thereupon", "these", "they", "this",
      "those", "though", "through", "throughout", "thru", "thus", "to",
      "together", "too", "toward", "towards", "under", "until", "up",
      "upon", "us", "very", "via", "was", "we", "well", "were", "what",
      "whatever", "whatsoever", "when", "whence", "whenever",
      "whensoever", "where", "whereafter", "whereas", "whereat",
      "whereby", "wherefrom", "wherein", "whereinto", "whereof",
      "whereon", "whereto", "whereunto", "whereupon", "wherever",
      "wherewith", "whether", "which", "whichever", "whichsoever",
      "while", "whilst", "whither", "who", "whoever", "whole", "whom",
      "whomever", "whomsoever", "whose", "whosoever", "why", "will",
      "with", "within", "without", "would", "xsubj", "xcal", "xauthor",
      "xother ", "xnote", "yet", "you", "your", "yours", "yourself",
      "yourselves"
    }, true));

        /**
         * A lower-casing word analyzer with English stop words (can be shared
         * freely across threads without harm); global per class loader.
         */
        public static readonly PatternAnalyzer DEFAULT_ANALYZER = new PatternAnalyzer(
          Version.LUCENE_CURRENT, NON_WORD_PATTERN, true, StopAnalyzer.ENGLISH_STOP_WORDS_SET);

        /**
         * A lower-casing word analyzer with <b>extended </b> English stop words
         * (can be shared freely across threads without harm); global per class
         * loader. The stop words are borrowed from
         * http://thomas.loc.gov/home/stopwords.html, see
         * http://thomas.loc.gov/home/all.about.inquery.html
         */
        public static readonly PatternAnalyzer EXTENDED_ANALYZER = new PatternAnalyzer(
          Version.LUCENE_CURRENT, NON_WORD_PATTERN, true, EXTENDED_ENGLISH_STOP_WORDS);

        private readonly Regex Regex;
        private readonly bool toLowerCase;
        private readonly ISet<string> stopWords;

        private readonly Version matchVersion;

        /**
         * Constructs a new instance with the given parameters.
         * 
         * @param matchVersion If >= {@link Version#LUCENE_29}, StopFilter.enablePositionIncrement is set to true
         * @param Regex
         *            a regular expression delimiting tokens
         * @param toLowerCase
         *            if <code>true</code> returns tokens after applying
         *            String.toLowerCase()
         * @param stopWords
         *            if non-null, ignores all tokens that are contained in the
         *            given stop set (after previously having applied toLowerCase()
         *            if applicable). For example, created via
         *            {@link StopFilter#makeStopSet(String[])}and/or
         *            {@link org.apache.lucene.analysis.WordlistLoader}as in
         *            <code>WordlistLoader.getWordSet(new File("samples/fulltext/stopwords.txt")</code>
         *            or <a href="http://www.unine.ch/info/clef/">other stop words
         *            lists </a>.
         */
        public PatternAnalyzer(Version matchVersion, Regex Regex, bool toLowerCase, ISet<string> stopWords)
        {
            if (Regex == null)
                throw new ArgumentException("Regex must not be null");

            if (EqRegex(NON_WORD_PATTERN, Regex)) Regex = NON_WORD_PATTERN;
            else if (EqRegex(WHITESPACE_PATTERN, Regex)) Regex = WHITESPACE_PATTERN;

            if (stopWords != null && stopWords.Count == 0) stopWords = null;

            this.Regex = Regex;
            this.toLowerCase = toLowerCase;
            this.stopWords = stopWords;
            this.matchVersion = matchVersion;
        }

        /**
         * Creates a token stream that tokenizes the given string into token terms
         * (aka words).
         * 
         * @param fieldName
         *            the name of the field to tokenize (currently ignored).
         * @param text
         *            the string to tokenize
         * @return a new token stream
         */
        public TokenStream TokenStream(String fieldName, String text)
        {
            // Ideally the Analyzer superclass should have a method with the same signature, 
            // with a default impl that simply delegates to the StringReader flavour. 
            if (text == null)
                throw new ArgumentException("text must not be null");

            TokenStream stream;
            if (Regex == NON_WORD_PATTERN)
            { // fast path
                stream = new FastStringTokenizer(text, true, toLowerCase, stopWords);
            }
            else if (Regex == WHITESPACE_PATTERN)
            { // fast path
                stream = new FastStringTokenizer(text, false, toLowerCase, stopWords);
            }
            else
            {
                stream = new RegexTokenizer(text, Regex, toLowerCase);
                if (stopWords != null) stream = new StopFilter(StopFilter.GetEnablePositionIncrementsVersionDefault(matchVersion), stream, stopWords);
            }

            return stream;
        }

        /**
         * Creates a token stream that tokenizes all the text in the given Reader;
         * This implementation forwards to <code>tokenStream(String, String)</code> and is
         * less efficient than <code>tokenStream(String, String)</code>.
         * 
         * @param fieldName
         *            the name of the field to tokenize (currently ignored).
         * @param reader
         *            the reader delivering the text
         * @return a new token stream
         */
        public override TokenStream TokenStream(String fieldName, TextReader reader)
        {
            if (reader is FastStringReader)
            { // fast path
                return TokenStream(fieldName, ((FastStringReader)reader).GetString());
            }

            try
            {
                String text = ToString(reader);
                return TokenStream(fieldName, text);
            }
            catch (IOException e)
            {
                throw new Exception("Wrapped Exception", e);
            }
        }

        /**
         * Indicates whether some other object is "equal to" this one.
         * 
         * @param other
         *            the reference object with which to compare.
         * @return true if equal, false otherwise
         */
        public override bool Equals(Object other)
        {
            if (this == other) return true;
            if (this == DEFAULT_ANALYZER && other == EXTENDED_ANALYZER) return false;
            if (other == DEFAULT_ANALYZER && this == EXTENDED_ANALYZER) return false;

            if (other is PatternAnalyzer)
            {
                PatternAnalyzer p2 = (PatternAnalyzer)other;
                return
                  toLowerCase == p2.toLowerCase &&
                  EqRegex(Regex, p2.Regex) &&
                  Eq(stopWords, p2.stopWords);
            }
            return false;
        }

        /**
         * Returns a hash code value for the object.
         * 
         * @return the hash code.
         */
        public override int GetHashCode()
        {
            if (this == DEFAULT_ANALYZER) return -1218418418; // fast path
            if (this == EXTENDED_ANALYZER) return 1303507063; // fast path

            int h = 1;
            h = 31 * h + Regex.GetHashCode();
            h = 31 * h + (int)Regex.Options;
            h = 31 * h + (toLowerCase ? 1231 : 1237);
            h = 31 * h + (stopWords != null ? stopWords.GetHashCode() : 0);
            return h;
        }

        /** equality where o1 and/or o2 can be null */
        private static bool Eq(Object o1, Object o2)
        {
            return (o1 == o2) || (o1 != null ? o1.Equals(o2) : false);
        }

        /** assumes p1 and p2 are not null */
        private static bool EqRegex(Regex p1, Regex p2)
        {
            return p1 == p2 || (p1.Options == p2.Options && p1.ToString() == p2.ToString());
        }

        /**
         * Reads until end-of-stream and returns all read chars, finally closes the stream.
         * 
         * @param input the input stream
         * @throws IOException if an I/O error occurs while reading the stream
         */
        private static String ToString(TextReader input)
        {
            try
            {
                int len = 256;
                char[] buffer = new char[len];
                char[] output = new char[len];

                len = 0;
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (len + n > output.Length)
                    { // grow capacity
                        char[] tmp = new char[Math.Max(output.Length << 1, len + n)];
                        Array.Copy(output, 0, tmp, 0, len);
                        Array.Copy(buffer, 0, tmp, len, n);
                        buffer = output; // use larger buffer for future larger bulk reads
                        output = tmp;
                    }
                    else
                    {
                        Array.Copy(buffer, 0, output, len, n);
                    }
                    len += n;
                }

                return new String(output, 0, len);
            }
            finally
            {
                if (input != null) input.Dispose();
            }
        }


        ///////////////////////////////////////////////////////////////////////////////
        // Nested classes:
        ///////////////////////////////////////////////////////////////////////////////
        /**
         * The work horse; performance isn't fantastic, but it's not nearly as bad
         * as one might think - kudos to the Sun regex developers.
         */
        private sealed class RegexTokenizer : TokenStream
        {

            private readonly String str;
            private readonly bool toLowerCase;
            private Match matcher;
            private int pos = 0;
            private static readonly System.Globalization.CultureInfo locale = System.Globalization.CultureInfo.CurrentCulture;
            private TermAttribute termAtt;
            private OffsetAttribute offsetAtt;

            public RegexTokenizer(String str, Regex regex, bool toLowerCase)
            {
                this.str = str;
                this.matcher = regex.Match(str);
                this.toLowerCase = toLowerCase;
                this.termAtt = AddAttribute<TermAttribute>();
                this.offsetAtt = AddAttribute<OffsetAttribute>();
            }

            public sealed override bool IncrementToken()
            {
                if (matcher == null) return false;
                ClearAttributes();
                while (true)
                { // loop takes care of leading and trailing boundary cases
                    int start = pos;
                    int end;
                    bool isMatch = matcher.Success;
                    if (isMatch)
                    {
                        end = matcher.Index;
                        pos = matcher.Index + matcher.Length;
                        matcher = matcher.NextMatch();
                    }
                    else
                    {
                        end = str.Length;
                        matcher = null; // we're finished
                    }

                    if (start != end)
                    { // non-empty match (header/trailer)
                        String text = str.Substring(start, end - start);
                        if (toLowerCase) text = text.ToLower(locale);
                        termAtt.SetTermBuffer(text);
                        offsetAtt.SetOffset(start, end);
                        return true;
                    }
                    return false;
                }
            }

            public override sealed void End()
            {
                // set final offset
                int finalOffset = str.Length;
                this.offsetAtt.SetOffset(finalOffset, finalOffset);
            }

            protected override void Dispose(bool disposing)
            {
                // Do Nothing
            }
        }


        ///////////////////////////////////////////////////////////////////////////////
        // Nested classes:
        ///////////////////////////////////////////////////////////////////////////////
        /**
         * Special-case class for best performance in common cases; this class is
         * otherwise unnecessary.
         */
        private sealed class FastStringTokenizer : TokenStream
        {

            private readonly String str;
            private int pos;
            private readonly bool isLetter;
            private readonly bool toLowerCase;
            private readonly ISet<string> stopWords;
            private static readonly System.Globalization.CultureInfo locale = System.Globalization.CultureInfo.CurrentCulture;
            private TermAttribute termAtt;
            private OffsetAttribute offsetAtt;

            public FastStringTokenizer(String str, bool isLetter, bool toLowerCase, ISet<string> stopWords)
            {
                this.str = str;
                this.isLetter = isLetter;
                this.toLowerCase = toLowerCase;
                this.stopWords = stopWords;
                this.termAtt = AddAttribute<TermAttribute>();
                this.offsetAtt = AddAttribute<OffsetAttribute>();
            }

            public override bool IncrementToken()
            {
                ClearAttributes();
                // cache loop instance vars (performance)
                String s = str;
                int len = s.Length;
                int i = pos;
                bool letter = isLetter;

                int start = 0;
                String text;
                do
                {
                    // find beginning of token
                    text = null;
                    while (i < len && !IsTokenChar(s[i], letter))
                    {
                        i++;
                    }

                    if (i < len)
                    { // found beginning; now find end of token
                        start = i;
                        while (i < len && IsTokenChar(s[i], letter))
                        {
                            i++;
                        }

                        text = s.Substring(start, i - start);
                        if (toLowerCase) text = text.ToLower(locale);
                        //          if (toLowerCase) {            
                        ////            use next line once JDK 1.5 String.toLowerCase() performance regression is fixed
                        ////            see http://bugs.sun.com/bugdatabase/view_bug.do?bug_id=6265809
                        //            text = s.substring(start, i).toLowerCase(); 
                        ////            char[] chars = new char[i-start];
                        ////            for (int j=start; j < i; j++) chars[j-start] = char.toLowerCase(s[j] );
                        ////            text = new String(chars);
                        //          } else {
                        //            text = s.substring(start, i);
                        //          }
                    }
                } while (text != null && IsStopWord(text));

                pos = i;
                if (text == null)
                {
                    return false;
                }
                termAtt.SetTermBuffer(text);
                offsetAtt.SetOffset(start, i);
                return true;
            }

            public override sealed void End()
            {
                // set final offset
                int finalOffset = str.Length;
                this.offsetAtt.SetOffset(finalOffset, finalOffset);
            }

            protected override void Dispose(bool disposing)
            {
                // Do Nothing
            }

            private bool IsTokenChar(char c, bool isLetter)
            {
                return isLetter ? char.IsLetter(c) : !char.IsWhiteSpace(c);
            }

            private bool IsStopWord(string text)
            {
                return stopWords != null && stopWords.Contains(text);
            }

        }


        ///////////////////////////////////////////////////////////////////////////////
        // Nested classes:
        ///////////////////////////////////////////////////////////////////////////////
        /**
         * A StringReader that exposes it's contained string for fast direct access.
         * Might make sense to generalize this to CharSequence and make it public?
         */
        internal sealed class FastStringReader : StringReader
        {

            private readonly string s;

            protected internal FastStringReader(string s)
                : base(s)
            {
                this.s = s;
            }

            internal string GetString()
            {
                return s;
            }
        }

    }
}