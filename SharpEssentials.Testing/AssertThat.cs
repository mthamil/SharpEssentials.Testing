// Sharp Essentials Testing
// Copyright 2017 Matthew Hamilton - matthamilton@live.com
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using SharpEssentials.Reflection;
using Xunit;
using Xunit.Sdk;

namespace SharpEssentials.Testing
{
    /// <summary>
    /// Contains custom assertions.
    /// </summary>
    public class AssertThat
    {
        /// <summary>
        /// Verifies that the provided object raised INotifyPropertyChanged.PropertyChanged as a result of executing the given
        /// test code.
        /// </summary>
        /// <typeparam name="TDeclaring">The type of object declaring the property</typeparam>
        /// <typeparam name="TValue">The value of the property</typeparam>
        /// <param name="object">The object declaring the property</param>
        /// <param name="property">The property</param>
        /// <param name="testCode">The code that should change the property</param>
        public static void PropertyChanged<TDeclaring, TValue>(TDeclaring @object, Expression<Func<TDeclaring, TValue>> property, Action testCode)
            where TDeclaring : INotifyPropertyChanged
        {
            Assert.PropertyChanged(@object, Reflect.PropertyOf(property).Name, testCode);
        }

        /// <summary>
        /// Verifies that the provided object did not raise INotifyPropertyChanged.PropertyChanged as a result of executing the given
        /// test code.
        /// </summary>
        /// <typeparam name="TDeclaring">The type of object declaring the property</typeparam>
        /// <typeparam name="TValue">The value of the property</typeparam>
        /// <param name="object">The object declaring the property</param>
        /// <param name="property">The property</param>
        /// <param name="testCode">The code that should not change the property</param>
        public static void PropertyDoesNotChange<TDeclaring, TValue>(TDeclaring @object, Expression<Func<TDeclaring, TValue>> property, Action testCode)
            where TDeclaring : INotifyPropertyChanged
        {
            object sender = null;
            PropertyChangedEventArgs args = null;
            void Handler(object o, PropertyChangedEventArgs e)
            {
                sender = o;
                args = e;
            }

            try
            {
                @object.PropertyChanged += Handler;
                testCode();
            }
            finally
            {
                @object.PropertyChanged -= Handler;
            }

            if (args != null && ReferenceEquals(sender, @object))
            {
                string propertyName = Reflect.PropertyOf(property).Name;
                if (args.PropertyName == propertyName)
                {
                    throw new PropertyDoesNotChangeException(propertyName);
                }
            }
        }

        /// <summary>
        /// Determines whether two sequences are equal by comparing length and element equality.
        /// The type of element's default equality comparer is used.
        /// </summary>
        /// <typeparam name="T">The type of elements in the sequences</typeparam>
        /// <param name="expected">The expected sequence</param>
        /// <param name="actual">The actual sequence</param>
        public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            SequenceEqual(expected, actual, EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Determines whether two sequences are equal by comparing length and element equality.
        /// </summary>
        /// <typeparam name="T">The type of elements in the sequences</typeparam>
        /// <param name="expected">The expected sequence</param>
        /// <param name="actual">The actual sequence</param>
        /// <param name="equalityComparer">The equality comparer to use for element equality. If null, the default for the type is used</param>
        public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, IEqualityComparer<T> equalityComparer)
        {
            IEqualityComparer<T> actualComparer = equalityComparer ?? EqualityComparer<T>.Default;

            // Cache the sequences so that they are not re-evaluated.
            ICollection<T> expectedBuffer = new List<T>();
            ICollection<T> actualBuffer = new List<T>();

            using (var expectedEnumerator = expected.GetEnumerator())
            using (var actualEnumerator = actual.GetEnumerator())
            {
                while (true)
                {
                    bool expectedHasNext = expectedEnumerator.MoveNext();
                    T expectedElement = default(T);
                    if (expectedHasNext)
                    {
                        expectedElement = expectedEnumerator.Current;
                        expectedBuffer.Add(expectedElement);
                    }

                    bool actualHasNext = actualEnumerator.MoveNext();
                    T actualElement = default(T);
                    if (actualHasNext)
                    {
                        actualElement = actualEnumerator.Current;
                        actualBuffer.Add(actualElement);
                    }

                    if (expectedHasNext != actualHasNext)
                        throw new SequenceEqualException(expectedBuffer, !expectedEnumerator.MoveNext(), actualBuffer, !actualEnumerator.MoveNext());

                    if (!actualHasNext || !expectedHasNext)
                        break;

                    if (!actualComparer.Equals(actualElement, expectedElement))
                        throw new SequenceEqualException(expectedBuffer, !expectedEnumerator.MoveNext(), actualBuffer, !actualEnumerator.MoveNext());
                }
            }
        }

        /// <summary>
        /// Asserts that the given event is raised.
        /// </summary>
        /// <param name="target">The object raising the event</param>
        /// <param name="subscriber">An action adding or removing a null handler from the target event</param>
        /// <param name="eventDelegate">The delegate to invoke which should trigger the event</param>
        public static void Raises<T>(T target, Action<T> subscriber, EventDelegate eventDelegate)
        {
            Raises(target, subscriber, eventDelegate, true);
        }

        /// <summary>
        /// Asserts that the given event is not raised.
        /// </summary>
        /// <param name="target">The object raising the event</param>
        /// <param name="subscriber">An action adding or removing a null handler from the target event</param>
        /// <param name="eventDelegate">The delegate to invoke which shouldn't trigger the event</param>
        public static void DoesNotRaise<T>(T target, Action<T> subscriber, EventDelegate eventDelegate)
        {
            Raises(target, subscriber, eventDelegate, false);
        }

        /// <summary>
        /// Asserts that an event is raised and returns the event arguments.
        /// </summary>
        /// <typeparam name="TTarget">The type of the target object</typeparam>
        /// <typeparam name="TArgs">The event argument type, must be of type EventArgs</typeparam>
        /// <param name="target">The object raising the event</param>
        /// <param name="subscriber">An action adding or removing a null handler from the target event</param>
        /// <param name="eventDelegate">The delegate to invoke which should trigger the event</param>
        /// <returns>The captured event args of the event if it is successfully raised</returns>
        public static TArgs RaisesWithEventArgs<TTarget, TArgs>(TTarget target, Action<TTarget> subscriber, EventDelegate eventDelegate)
            where TArgs : EventArgs
        {
            EventProxy proxy = Raises(target, subscriber, eventDelegate, true);
            return (TArgs)proxy.ArgsReceived;
        }

        private static EventProxy Raises<T>(T target, Action<T> subscriber, EventDelegate eventDelegate, bool expectEventReceived)
        {
            EventInfo eventInfo = ExtractEvent(subscriber, target);
            EventProxy proxy = InternalRaises(target, eventInfo, eventDelegate);

            if (expectEventReceived != proxy.EventReceived)
                throw new RaisesException(typeof(T), eventInfo.Name, expectEventReceived, proxy.EventReceived);

            return proxy;
        }

        private static EventInfo ExtractEvent<T>(Action<T> eventAccessor, T target)
        {
            var recorder = new MethodRecorder<T>();
            eventAccessor(recorder.Proxy);

            IMethodCallMessage eventMember = recorder.LastInvocation;
            if (!(eventMember.MethodName.StartsWith("add_") || eventMember.MethodName.StartsWith("remove_")))
                throw new ArgumentException(@"Invocation must be an event subscription or unsubscription", nameof(eventAccessor));

            string eventName = eventMember.MethodName.Replace("add_", string.Empty).Replace("remove_", string.Empty);
            EventInfo eventInfo = typeof(T).GetEvent(eventName);
            return eventInfo;
        }

        /// <summary>
        /// Asserts that the given event is raised.
        /// </summary>
        /// <param name="target">The object raising the event</param>
        /// <param name="eventName">The name of the event</param>
        /// <param name="eventDelegate">The delegate to invoke which should trigger the event</param>
        public static void Raises(object target, string eventName, EventDelegate eventDelegate)
        {
            Raises(target, eventName, eventDelegate, true);
        }

        /// <summary>
        /// Asserts that the given event is not raised.
        /// </summary>
        /// <param name="target">The object raising the event</param>
        /// <param name="eventName">The name of the event</param>
        /// <param name="eventDelegate">The delegate to invoke which shouldn't trigger the event</param>
        public static void DoesNotRaise(object target, string eventName, EventDelegate eventDelegate)
        {
            Raises(target, eventName, eventDelegate, false);
        }

        /// <summary>
        /// Asserts that an event is raised and returns the event arguments.
        /// </summary>
        /// <typeparam name="TArgs">The event argument type, must be of type EventArgs</typeparam>
        /// <param name="target">The object raising the event</param>
        /// <param name="eventName">The name of the event</param>
        /// <param name="eventDelegate">The delegate to invoke which should trigger the event</param>
        /// <returns>The captured event args of the event if it is successfully raised</returns>
        public static TArgs RaisesWithEventArgs<TArgs>(object target, string eventName, EventDelegate eventDelegate)
            where TArgs : EventArgs
        {
            EventProxy proxy = Raises(target, eventName, eventDelegate, true);
            return (TArgs)proxy.ArgsReceived;
        }

        private static EventProxy Raises(object target, string eventName, EventDelegate eventDelegate, bool expectEventReceived)
        {
            Type targetType = target.GetType();
            EventInfo eventInfo = targetType.GetEvent(eventName);
            EventProxy proxy = InternalRaises(target, eventInfo, eventDelegate);

            if (expectEventReceived != proxy.EventReceived)
                throw new RaisesException(targetType, eventName, expectEventReceived, proxy.EventReceived);

            return proxy;
        }

        private static EventProxy InternalRaises<T>(T target, EventInfo eventInfo, EventDelegate eventDelegate)
        {
            // this method to dynamically subscribe for events was taken from 
            // http://www.codeproject.com/KB/cs/eventtracingviareflection.aspx
            EventProxy proxy = new EventProxy();
            Delegate d = Delegate.CreateDelegate(eventInfo.EventHandlerType, proxy, EventProxy.Handler);

            eventInfo.AddEventHandler(target, d);
            eventDelegate.DynamicInvoke();
            eventInfo.RemoveEventHandler(target, d);

            return proxy;
        }

        /// <summary>
        /// Delegate for code that should raise an event.
        /// </summary>
        public delegate void EventDelegate();

        private class EventProxy
        {
            public void OnEvent(object sender, EventArgs e)
            {
                EventReceived = true;
                ArgsReceived = e;
            }

            public bool EventReceived { get; private set; }
            public EventArgs ArgsReceived { get; private set; }

            public static readonly MethodInfo Handler = Reflect.MethodOf<EventProxy>(ep => ep.OnEvent(null, null));
        }
    }

    /// <summary>
    /// Exception raised when an assertion about the raising of an event is not met.
    /// </summary>
    public class RaisesException : XunitException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="RaisesException"/> class.
        /// </summary>
        /// <param name="eventOwner">The type of object owning the event</param>
        /// <param name="eventName">The name of the event</param>
        /// <param name="shouldHaveBeenRaised">Whether the event should have been raised</param>
        /// <param name="wasRaised">Whether the event was raised</param>
        public RaisesException(Type eventOwner,
                               string eventName,
                               bool shouldHaveBeenRaised,
                               bool wasRaised)
            : base($"{typeof(RaisesException).Name} : Event Assertion Failure")
        {
            EventOwner = eventOwner;
            EventName = eventName;
            ShouldHaveBeenRaised = shouldHaveBeenRaised;
            WasRaised = wasRaised;
        }

        /// <summary>
        /// The type of object owning the event.
        /// </summary>
        public Type EventOwner { get; }

        /// <summary>
        /// The name of the event.
        /// </summary>
        public string EventName { get; }

        /// <summary>
        /// Whether the event should have been raised.
        /// </summary>
        public bool ShouldHaveBeenRaised { get; }

        /// <summary>
        /// Whether the event was raised.
        /// </summary>
        public bool WasRaised { get; }

        /// <summary>
        /// Gets a message that describes the current exception.
        /// </summary>
        /// <returns>The error message that explains the reason for the exception, or an empty string("").</returns>
        public override string Message =>
            String.Format("{0}{1}The event {2} {3} when {4}.",
                           base.Message,
                           Environment.NewLine,
                           EventName,
                           WasRaised ? "was raised" : "was not raised",
                           ShouldHaveBeenRaised ? "expected" : "not expected");
    }

    /// <summary>
    /// Exceptions thrown when a SequenceEqual assertion fails.
    /// </summary>
    public class SequenceEqualException : XunitException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="SequenceEqualException"/> class.
        /// </summary>
        /// <param name="expected">The expected sequence</param>
        /// <param name="expectedFullyDrained">Whether the entirety of the expected sequence was evaluated</param>
        /// <param name="actual">The actual sequence</param>
        /// <param name="actualFullyDrained">Whether the entirety of the actual sequence was evaluated</param>
        public SequenceEqualException(IEnumerable expected, bool expectedFullyDrained, IEnumerable actual, bool actualFullyDrained)
            : base($"{typeof(SequenceEqualException).Name} : SequenceEqual Assertion Failure")
        {
            _expected = expected;
            _actual = actual;
            _expectedFullyDrained = expectedFullyDrained;
            _actualFullyDrained = actualFullyDrained;

            Actual = String.Join(",", _actual.Cast<object>());
            Expected = String.Join(",", _expected.Cast<object>());
        }

        /// <summary>
        /// Gets the actual sequence.
        /// </summary>
        public string Actual { get; }

        /// <summary>
        /// Gets the expected sequence.
        /// </summary>
        public string Expected { get; }

        /// <summary>
        /// Gets a message that describes the current exception.
        /// </summary>
        /// <returns>The error message that explains the reason for the exception, or an empty string("").</returns>
        public override string Message => 
            String.Format("{0}{1}Expected: {2}{3}{1}Actual: {4}{5}",
                           base.Message,
                           Environment.NewLine,
                           Expected == string.Empty ? EMPTY_COLLECTION_MESSAGE : Expected,
                           _expectedFullyDrained ? string.Empty : ",...",
                           Actual == string.Empty ? EMPTY_COLLECTION_MESSAGE : Actual,
                           _actualFullyDrained ? string.Empty : ",...");

        private readonly IEnumerable _expected;
        private readonly IEnumerable _actual;
        private readonly bool _expectedFullyDrained;
        private readonly bool _actualFullyDrained;

        private const string EMPTY_COLLECTION_MESSAGE = "Empty Sequence";
    }

    /// <summary>
    /// Exception thrown when code unexpectedly changes a property.
    /// </summary>
    [Serializable]
    public class PropertyDoesNotChangeException : XunitException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="T:SharpEssentials.Testing.PropertyDoesNotChangeException"/> class.
        /// </summary>
        /// <param name="propertyName">The name of the property that should not have changed.</param>
        public PropertyDoesNotChangeException(string propertyName)
            : base($"PropertyDoesNotChange assertion failure: PropertyChanged event for property {propertyName} was raised")
        {
        }
    }
}