﻿[<AutoOpen>]
module FSharp.Monad.Iteratee.Core

/// A stream of chunks of data generated by an Enumerator.
/// The stream can be composed of chunks of 'a, empty blocks indicating a wait, or an EOF marker.
/// In Haskell, the Chunk is usually composed of a list of ListLike type, but F# doesn't support
/// Monad Transforms or ^M in type declarations. Thus, the Chunk is left open to various internal
/// types, but a bit more work must be done in order to maintain the meaningfulness of "chunk".
/// That said, the 'a allows a large number of chunk-y types to be used, including other monads.
/// Be aware that when using #seq<_> types, you will need to check for both Seq.empty ([]) and Empty.
type Stream<'a> =
  | Chunk of 'a
  | Empty
  | EOF

// TODO: Stream Monoid? I had this when using Chunk of 'a list, but trying to create a generic monoid appears impossible. I'll have to add multiple versions of everything in order to support such a construct. The Empty discriminator provides much of what was needed.

// TODO: Stream Monad?

/// The iteratee is a stream consumer that will consume a stream of data until either 
/// it receives an EOF or meets its own requirements for consuming data. The iteratee
/// will return Continue whenever it is ready to receive the next chunk. An iteratee
/// is fed data by an Enumerator, which generates a Stream. 
type Iteratee<'el,'a> =
  | Yield of 'a * Stream<'el>
  | Error of exn
  | Continue of (Stream<'el> -> Iteratee<'el,'a>)

/// An enumerator generates a stream of data and feeds an iteratee, returning a new iteratee.
type Enumerator<'el,'a> = Iteratee<'el,'a> -> Iteratee<'el,'a>

/// An Enumeratee is an Enumerator that feeds data streams to an internal iteratee.
type Enumeratee<'elo,'eli,'a> = Iteratee<'eli,'a> -> Iteratee<'elo, Iteratee<'eli,'a>>

// TODO: Make calls to bind tail recursive.
let bind m f =
  let rec innerBind m =
    match m with
    | Continue k -> Continue(innerBind << k)
    | Error e -> Error e
    | Yield(x, Empty) -> f x
    | Yield(x, extra) ->
        match f x with
        | Continue k -> k extra
        | Error e -> Error e
        | Yield(acc',_) -> Yield(acc', extra)
  innerBind m

let combine comp1 comp2 =
  let binder () = comp2
  bind comp1 binder

type IterateeBuilder() =
  member this.Return(x) = Yield(x, Empty)
  member this.ReturnFrom(m:Iteratee<_,_>) = m
  member this.Bind(m, k) = bind m k
  member this.Zero() = Yield((), Empty)
  member this.Combine(comp1, comp2) = combine comp1 comp2
  member this.Delay(f) = bind (Yield((), Empty)) f
let iteratee = IterateeBuilder()

module Operators =
  open FSharp.Monad.Operators

  let inline returnM x = returnM iteratee x
  let inline (>>=) m f = bindM iteratee m f
  let inline (<*>) f m = applyM iteratee iteratee f m
  let inline lift f m = liftM iteratee f m
  let inline (<!>) f m = lift f m
  let inline lift2 f a b = returnM f <*> a <*> b
  let inline ( *>) x y = lift2 (fun _ z -> z) x y
  let inline ( <*) x y = lift2 (fun z _ -> z) x y
  let inline (>>.) m f = m >>= (fun _ -> f)

let rec enumEOF = function
  | Yield(x,_) -> Yield(x,EOF)
  | Error e -> Error e
  | Continue k ->
      match k EOF with
      | Continue _ -> failwith "enumEOF: divergent iteratee"
      | i -> enumEOF i

let run i =
  match enumEOF i with
  | Error e -> Choice1Of2 e
  | Yield(x,_) -> Choice2Of2 x
  | Continue _ -> failwith "run: divergent iteratee"

let run_ i =
  match run i with
  | Choice1Of2 e -> raise e
  | x -> x
