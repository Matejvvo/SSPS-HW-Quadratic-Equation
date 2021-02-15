﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Equations
{
    public struct Variable
    {
        private const string PARSE_NUMBER_PATTERN = @"[+-]?\d*([.,]\d+)?";
        private static string ParseIdentifierPattern = $@"[a-zA-Z](\^{ PARSE_NUMBER_PATTERN })?";
        private static string IsStringVariablePattern { get => $@"^((?=.+)({ PARSE_NUMBER_PATTERN })?({ ParseIdentifierPattern })?)+$"; }
        private static string IsInStringVariablePattern { get => $@"(?=.+)({ PARSE_NUMBER_PATTERN })?({ ParseIdentifierPattern })?"; }

        public double Multiplier { get; }

        public VariableIdentifierCollection Identifiers { get; }

        public Variable(char[] markers, double[] exponents, double multiplier)
        {
            Multiplier = multiplier;

            if (multiplier == 0)
            {
                markers = new char[0];
                exponents = new double[0];
            }

            Identifiers = CreateIdentifiers(exponents, markers);
        }

        public Variable(VariableIdentifierCollection identifiers, double multiplier)
        {
            Multiplier = multiplier;
            if(multiplier == 0)
            {
                Identifiers = CreateIdentifiers(new double[0], new char[0]);
                return;
            }
            Identifiers = identifiers.Clone();
        }

        private static VariableIdentifierCollection CreateIdentifiers(double[] exponents, char[] markers)
        {
            if (markers.Length != exponents.Length)
                throw new Exception("Something went wrong. Not every marker has its own exponent!");

            VariableIdentifier[] _identifiers = new VariableIdentifier[markers.Length];
            for (int i = 0; i < markers.Length; i++)
            {
                _identifiers[i] = new VariableIdentifier(markers[i], exponents[i]);
            }

            return new VariableIdentifierCollection(_identifiers);
        }

        public static Variable Parse(string s)
        {
            if (s.Contains('/'))
                throw new NotImplementedException("Polynomials are not yet implemented!");

            if (!IsStringVariable(s, out MatchCollection matches))
                throw new FormatException();

            List<Variable> variables = new List<Variable>();

            foreach (Match _match in matches)
            {
                Match match = Regex.Match(_match.Value, IsInStringVariablePattern);
                string multiplierString = match.Groups[1].Value;
                double multiplier = 1;
                if (multiplierString == "-")
                    multiplier = -1;
                else if (multiplierString != string.Empty && multiplierString != "+")
                    multiplier = double.Parse(multiplierString, CultureInfo.InvariantCulture);

                //Console.WriteLine(match.Groups[2].Value);

                string identifier = match.Groups[3].Value;
                char? marker = null;
                if (identifier != string.Empty)
                    marker = identifier[0];

                double exponent = 1;
                if (identifier.Length >= 3)
                    exponent = double.Parse(identifier.Substring(2, identifier.Length - 2), CultureInfo.InvariantCulture);

                variables.Add(new Variable(marker.HasValue ? new[] { marker.Value } : new char[0], marker.HasValue ? new[] { exponent } : new double[0], multiplier));
            }

            if (variables.Count == 1)
                return variables[0];

            return MultiplyDifferentVariables(variables);
        }

        private static Variable MultiplyDifferentVariables(IEnumerable<Variable> variables)
        {
            List<VariableIdentifier> identifiers = new List<VariableIdentifier>();
            double finalMultiplier = 1;

            foreach (Variable variable in variables)
            {
                finalMultiplier *= variable.Multiplier;

                identifiers.AddRange(variable.Identifiers);
            }


            Dictionary<char, double> variableDataFinal = new Dictionary<char, double>();
            foreach (VariableIdentifier identifier in identifiers)
            {
                if (!variableDataFinal.ContainsKey(identifier.Marker))
                    variableDataFinal.Add(identifier.Marker, identifier.Exponent);
                else
                    variableDataFinal[identifier.Marker] += identifier.Exponent;
            }

            List<VariableIdentifier> finalIdentifiers = new List<VariableIdentifier>();

            foreach (KeyValuePair<char, double> variableData in variableDataFinal)
            {
                finalIdentifiers.Add(new VariableIdentifier(variableData.Key, variableData.Value));
            }

            return new Variable(new VariableIdentifierCollection(finalIdentifiers.ToArray()), finalMultiplier);
        }

        private static bool IsStringVariable(string s, out MatchCollection matches)
        {
            matches = Regex.Matches(s, IsInStringVariablePattern);
            int expectedIndex = 0;
            foreach (Match ii in matches)
            {
                if (ii.Index != expectedIndex)
                    return false;
                expectedIndex += ii.Length;
            }
            return matches.Count > 0 && expectedIndex == s.Length;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException();

            if (!(obj is Variable))
                return false;

            Variable var = (Variable)obj;

            return Multiplier == var.Multiplier && 
                Identifiers.Equals(var.Identifiers);
        }

        public override int GetHashCode()
        {
            int hashCode = -1645715709;
            hashCode = hashCode * -1521134295 + Identifiers.GetHashCode();
            hashCode = hashCode * -1521134295 + Multiplier.GetHashCode();
            return hashCode;
        }

        public static VariableCollection operator +(Variable a, Variable b)
        {
            if (a.Identifiers.Equals(b.Identifiers))
            {
                return new Variable(a.Identifiers, a.Multiplier + b.Multiplier);
            }
            return new VariableCollection(a, b);
        }

        public static Variable operator -(Variable a)
        {
            return new Variable(a.Identifiers, -a.Multiplier);
        }

        public static VariableCollection operator -(Variable a, Variable b)
        {
            return a + -b;
        }

        public static Variable operator *(Variable a, Variable b)
        {
            char[] aMarkers = a.Identifiers.GetMarkers();
            char[] bMarkers = b.Identifiers.GetMarkers();
            if (a.Identifiers.Count == 1 && a.Identifiers.HasSameMarkersAs(b.Identifiers))
                return new Variable(aMarkers, new[] { a.Identifiers.GetExponents()[0] + b.Identifiers.GetExponents()[0] }, a.Multiplier * b.Multiplier);
            if(a.Identifiers.Count == 0 || b.Identifiers.Count == 0)
            {
                Variable variable = b;
                Variable multiplier = a;
                if(b.Identifiers.Count == 0)
                {
                    variable = a;
                    multiplier = b;
                }
                return new Variable(variable.Identifiers, variable.Multiplier * multiplier.Multiplier);
            }

            return MultiplyDifferentVariables(new[] { a, b });
        }

        public static Variable operator /(Variable a, Variable b)
        {
            if (b.Identifiers.Count > 0)
                throw new NotImplementedException("Dividing by a variable is not yet implemented!");

            if (b.Multiplier == 0)
                throw new DivideByZeroException();

            return new Variable(a.Identifiers, a.Multiplier / b.Multiplier);
        }

        public static implicit operator Variable(double number)
        {
            return new Variable(new char[0], new double[0], number);
        }

        public static explicit operator double(Variable variable)
        {
            if (variable.Identifiers.Count > 0)
                throw new ArgumentException("Inputed variable is not a raw number!");

            return variable.Multiplier;
        }

        public override string ToString() => ToString(false);

        internal string ToString(bool withoutSigns)
        {
            double val = Multiplier;
            if (withoutSigns)
                val = Math.Abs(Multiplier);

            if (Identifiers.Count <= 0)
                return val.ToString();
            if (val == 1 && Identifiers.Count > 0)
                return Identifiers.ToString();
            else if (val == 1)
                return "1";
            if (val == -1)
                return "-" + Identifiers;

            return (val + Identifiers.ToString()).Replace(',', '.');
        }
    }
}
