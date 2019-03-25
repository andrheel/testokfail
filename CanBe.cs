using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public static class Canbe
    {
        /// <summary>
        /// 
        /// </summary>
        public class Error
        {
            public int Code { get; set; }
            public string Message { get; set; }
            public Exception Exn { get; set; }
            public static Error From(Exception exn) 
                    => new Error() { 
                        Exn = exn,
                        Code = 500,
                        Message = exn.Message };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class ICanbe<T>
        {
            public T Value { get; set; }
            public Error Error { get; set; } = null;

            public bool IsOk => Error == null;
            public bool IsError => !IsOk;
            public override string ToString() => IsOk ? $"OK:{Value}" : $"ERROR:{Error.Message}";
        }

        #region Cons`es
        public static ICanbe<T> Cons<T>(T value) => new ICanbe<T>() { Value = value };
        public static ICanbe<T> Fail<T>(Error exn) => new ICanbe<T>() { Error = exn };
        public static ICanbe<T> Fail<T>(string message, int hresult)
                => Fail<T>(new Error() { Code = hresult, Message = message });
        #endregion

        #region Bind
        public static ICanbe<O> Bind<I, O>(this ICanbe<I> self, Func<I, ICanbe<O>> func)
        {
            try { return self.IsError ? Fail<O>(self.Error) : func(self.Value); }
            catch (Exception exn) { return Fail<O>(Error.From(exn)); }
        }
        async public static Task<ICanbe<O>> Bind<I, O>(this Task<ICanbe<I>> self, Func<I, ICanbe<O>> func)
                => (await self).Bind(func);
        async public static Task<ICanbe<O>> Bind<I, O>(this ICanbe<I> self, Func<I, Task<ICanbe<O>>> func)
        {
            try { return self.IsError ? Fail<O>(self.Error) : await func(self.Value); }
            catch (Exception exn) { return Fail<O>(Error.From(exn)); }
        }
        async public static Task<ICanbe<O>> Bind<I, O>(this Task<ICanbe<I>> aself, Func<I, Task<ICanbe<O>>> func)
        {
            var self = await aself;
            try { return self.IsError ? Fail<O>(self.Error) : await func(self.Value); }
            catch (Exception exn) { return Fail<O>(Error.From(exn)); }
        }
        #endregion

        #region Map
        public static ICanbe<O> Map<I, O>(this ICanbe<I> self, Func<I, O> func)
        => self.Bind(x => Cons(func(x)));
        async public static Task<ICanbe<O>> Map<I, O>(this Task<ICanbe<I>> self, Func<I, O> func)
                => (await self).Bind(x => Cons(func(x)));
        async public static Task<ICanbe<O>> Map<I, O>(this Task<ICanbe<I>> self, Func<I, Task<O>> func)
                => await (await self).Bind(async x => Cons(await func(x)));
        async public static Task<ICanbe<O>> Map<I, O>(this ICanbe<I> self, Func<I, Task<O>> func)
                => await self.Bind(async x => Cons(await func(x)));
        #endregion

        #region On Fail
        public static ICanbe<I> OnFail<I>(this ICanbe<I> self, Action<Error> func)
        {
            try
            {
                if (self.IsError) func(self.Error);
                return self;
            }
            catch (Exception exn) { return Fail<I>(Error.From(exn)); }
        }
        async public static Task<ICanbe<I>> OnFail<I>(this Task<ICanbe<I>> self, Action<Error> func)
                => (await self).OnFail(func);
        #endregion

        #region On Succ
        public static ICanbe<I> OnSucc<I>(this ICanbe<I> self, Action<I> func)
        {
            try
            {
                if (self.IsOk) func(self.Value);
                return self;
            }
            catch (Exception exn) { return Fail<I>(Error.From(exn)); }
        }
        async public static Task<ICanbe<I>> OnSucc<I>(this Task<ICanbe<I>> self, Action<I> func)
                => (await self).OnSucc(func);
        #endregion

        #region Fail If
        public static ICanbe<I> FailIf<I>(this ICanbe<I> self, Func<I, bool> func, Func<I, string> lazymessage, int errorcode = 0)
        {
            try
            {
                if (self.IsOk && func(self.Value)) return Fail<I>(lazymessage(self.Value), errorcode);
                return self;
            }
            catch (Exception exn) { return Fail<I>(Error.From(exn)); }
        }

        public static ICanbe<I> FailIf<I>(this ICanbe<I> self, Func<I, bool> func, string message, int errorcode = 0)
        {
            try
            {
                if (self.IsOk && func(self.Value)) return Fail<I>(message, errorcode);
                return self;
            }
            catch (Exception exn) { return Fail<I>(Error.From(exn)); }
        }
        public static ICanbe<I> FailIf<I>(this ICanbe<I> self, Func<I, bool> func, string message, System.Net.HttpStatusCode errorcode)
                    => FailIf<I>(self, func, message, (int)errorcode);
        public static ICanbe<I> FailIf<I>(this ICanbe<I> self, Func<I, bool> func, Func<I, string> lazymessage, System.Net.HttpStatusCode errorcode)
                    => FailIf<I>(self, func, lazymessage, (int)errorcode);

        async public static Task<ICanbe<I>> FailIf<I>(this Task<ICanbe<I>> self, Func<I, bool> func, string message)
                => (await self).FailIf(func, message);
        #endregion

        public static ICanbe<I> FailOnNull<I>(this ICanbe<I> self, string message, int code = 0)
            => self.FailIf(xs => xs == null, message, code);
        public static ICanbe<I> FailOnNull<I>(this ICanbe<I> self, string message, System.Net.HttpStatusCode code)
            => FailOnNull(self, message, (int)code);
        public static ICanbe<I> FailOnNull<I>(this ICanbe<I> self, Func<I, string> lazymessage, System.Net.HttpStatusCode code)
            => FailIf(self, (xs => xs == null), lazymessage, (int)code);

        #region FailOnEmpty
        public static ICanbe<IEnumerable<I>> FailOnEmpty<I>(this ICanbe<IEnumerable<I>> self, string message, int code = 0)
    => self.FailIf(xs => xs == null || (!xs.Any()), message, code);
        public static ICanbe<IEnumerable<I>> FailOnEmpty<I>(this ICanbe<IEnumerable<I>> self, string message, System.Net.HttpStatusCode code)
            => FailOnEmpty(self, message, (int)code);
        public static ICanbe<IEnumerable<I>> FailOnEmpty<I>(this ICanbe<IEnumerable<I>> self, Func<IEnumerable<I>,string> lazymessage, System.Net.HttpStatusCode code)
            => FailIf(self, (xs => xs == null || (!xs.Any())), lazymessage, (int)code);
        public static ICanbe<I[]> FailOnEmpty<I>(this ICanbe<I[]> self, string message, int code = 0)
            => self.FailIf(xs => xs == null || (!xs.Any()), message, code);
        public static ICanbe<I[]> FailOnEmpty<I>(this ICanbe<I[]> self, string message, System.Net.HttpStatusCode code)
            => FailOnEmpty(self, message, (int)code);
        public static ICanbe<I[]> FailOnEmpty<I>(this ICanbe<I[]> self, Func<I[], string> lazymessage, System.Net.HttpStatusCode code)
            => FailIf(self, (xs => xs == null || (!xs.Any())), lazymessage, (int)code);
        #endregion


        #region Reinit
        public static ICanbe<I> Reinit<I>(this ICanbe<I> self, Func<I> def)
        {
            try
            {
                if (self.IsError) return Cons<I>(def());
                return self;
            }
            catch (Exception exn) { return Fail<I>(Error.From(exn)); }
        }
        async public static Task<ICanbe<I>> Reinit<I>(this Task<ICanbe<I>> self, Func<I> def)
                => (await self).Reinit(def);
        #endregion


    }


}
