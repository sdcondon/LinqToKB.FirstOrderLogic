﻿using LinqToKB.FirstOrderLogic.InternalUtilities;
using System.Collections.Generic;
using System.Linq;

namespace LinqToKB.FirstOrderLogic.Sentences.Manipulation
{
    public class SentenceUnifier
    {
        public bool TryUnify(
            Sentence x,
            Sentence y,
            out IDictionary<Variable, Term> unifier)
        {
            unifier = new Dictionary<Variable, Term>();

            if (!TryUnify(x, y, unifier))
            {
                unifier = null;
                return false;
            }

            return true;
        }

        private bool TryUnify(
            Sentence x,
            Sentence y,
            IDictionary<Variable, Term> unifier)
        {
            // TODO-PERFORMANCE: Given the fundamentality of unification and the number of times that this could be called during inference,
            // it might be worth optimising it a little via a visitor-style design instead of this type switch..
            return (x, y) switch
            {
                (Conjunction conjunctionX, Conjunction conjunctionY) => TryUnify(conjunctionX, conjunctionY, unifier),
                (Disjunction disjunctionX, Disjunction disjunctionY) => TryUnify(disjunctionX, disjunctionY, unifier),
                (Equality equalityX, Equality equalityY) => TryUnify(equalityX, equalityY, unifier),
                (Equivalence equivalenceX, Equivalence equivalenceY) => TryUnify(equivalenceX, equivalenceY, unifier),
                //(ExistentialQuantification existentialQuantificationX, ExistentialQuantification existentialQuantificationY) => TryUnify(existentialQuantificationX, existentialQuantificationY, unifier),
                (Implication implicationX, Implication implicationY) => TryUnify(implicationX, implicationY, unifier),
                (Negation negationX, Negation negationY) => TryUnify(negationX, negationY, unifier),
                (Predicate predicateX, Predicate predicateY) => TryUnify(predicateX, predicateY, unifier),
                //(UniversalQuantification universalQuantificationX, UniversalQuantification universalQuantificationY) => TryUnify(universalQuantificationX, universalQuantificationY, unifier),
                _ => false
            };
        }

        private bool TryUnify(
            Conjunction x,
            Conjunction y,
            IDictionary<Variable, Term> unifier)
        {
            // BUG: Order shouldn't matter (but need to be careful about partially updating unifier)
            // perhaps Low and High (internal) props in conjunction?
            return TryUnify(x.Left, y.Left, unifier) && TryUnify(x.Right, y.Right, unifier);
        }

        public bool TryUnify(
            Disjunction x,
            Disjunction y,
            IDictionary<Variable, Term> unifier)
        {
            // BUG: Order shouldn't matter (but need to be careful about partially updating unifier)
            // perhaps Low and High (internal) props in conjunction?
            return TryUnify(x.Left, y.Left, unifier) && TryUnify(x.Right, y.Right, unifier);
        }

        public bool TryUnify(
            Equality x,
            Equality y,
            IDictionary<Variable, Term> unifier)
        {
            // BUG: Order shouldn't matter (but need to be careful about partially updating unifier)
            // perhaps Low and High (internal) props in conjunction?
            return TryUnify(x.Left, y.Left, unifier) && TryUnify(x.Right, y.Right, unifier);
        }

        public bool TryUnify(
            Equivalence x,
            Equivalence y,
            IDictionary<Variable, Term> unifier)
        {
            // BUG: Order shouldn't matter (but need to be careful about partially updating unifier)
            // perhaps Low and High (internal) props in conjunction?
            return TryUnify(x.Left, y.Left, unifier) && TryUnify(x.Right, y.Right, unifier);
        }

        ////public virtual Sentence TryUnify(
        ////    ExistentialQuantification x,
        ////    ExistentialQuantification y,
        ////    IDictionary<Variable, Term> unifier)
        ////{
        ////    var variable = ApplyToVariableDeclaration(existentialQuantification.Variable);
        ////    var sentence = ApplyToSentence(existentialQuantification.Sentence);
        ////    if (variable != existentialQuantification.Variable || sentence != existentialQuantification.Sentence)
        ////    {
        ////        return new ExistentialQuantification(variable, sentence);
        ////    }
        ////
        ////    return existentialQuantification;
        ////}

        public bool TryUnify(
            Implication x,
            Implication y,
            IDictionary<Variable, Term> unifier)
        {
            return TryUnify(x.Antecedent, y.Antecedent, unifier) && TryUnify(x.Consequent, y.Consequent, unifier);
        }

        public bool TryUnify(
            Negation x,
            Negation y,
            IDictionary<Variable, Term> unifier)
        {
            return TryUnify(x.Sentence, y.Sentence, unifier);
        }

        public bool TryUnify(
            Predicate x,
            Predicate y,
            IDictionary<Variable, Term> unifier)
        {
            if (!x.SymbolEquals(y))
            {
                return false;
            }

            foreach (var args in x.Arguments.Zip(y.Arguments, (x, y) => (x, y)))
            {
                if (!TryUnify(args.x, args.y, unifier))
                {
                    return false;
                }
            }

            return true;
        }

        ////public bool TryUnify(
        ////    UniversalQuantification x,
        ////    UniversalQuantification y,
        ////    IDictionary<Variable, Term> unifier)
        ////{
        ////    var variable = ApplyToVariableDeclaration(universalQuantification.Variable);
        ////    var sentence = ApplyToSentence(universalQuantification.Sentence);
        ////    if (variable != universalQuantification.Variable || sentence != universalQuantification.Sentence)
        ////    {
        ////        return new UniversalQuantification(variable, sentence);
        ////    }
        ////
        ////    return universalQuantification;
        ////}

        public bool TryUnify(
            Term x,
            Term y,
            IDictionary<Variable, Term> unifier)
        {
            return (x, y) switch
            {
                (Variable variable, _) => TryUnify(variable, y, unifier),
                (_, Variable variable) => TryUnify(variable, x, unifier),
                (Function functionX, Function functionY) => TryUnify(functionX, functionY, unifier),
                _ => x.Equals(y), // only potential for equality is if they're both constants. Worth being explicit?
            };
        }

        public bool TryUnify(
            Variable variable,
            Term other,
            IDictionary<Variable, Term> unifier)
        {
            if (unifier.TryGetValue(variable, out var value))
            {
                return TryUnify(other, value, unifier);
            }
            else if (other is Variable otherVariable && unifier.TryGetValue(otherVariable, out value))
            {
                return TryUnify(variable, value, unifier);
            }
            ////else if (Occurs(variable, other))
            ////{
            ////    return false;
            ////}
            else
            {
                unifier[variable] = other;
                return true;
            }
        }

        public bool TryUnify(
            Function x,
            Function y,
            IDictionary<Variable, Term> unifier)
        {
            // Dunno if this is the right way to unify (i.e. skolems can't unify with non-skolems?).. More reading and time will tell, but wouldn't be surprised if this needs to change..
            if ((x is MemberFunction domainX && y is MemberFunction domainY && !MemberInfoEqualityComparer.Instance.Equals(domainX.Member, domainY.Member))
                || (x is SkolemFunction skolemX && y is SkolemFunction skolemY && skolemX.Label.Equals(skolemY.Label)))
            {
                foreach (var args in x.Arguments.Zip(y.Arguments, (x, y) => (x, y)))
                {
                    if (!TryUnify(args.x, args.y, unifier))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
