open System

type ChoiceBuilder() =
    member this.Return x = Some (Choice1Of2 x)
    member this.Bind (p, rest) =
        match p with
        | Choice2Of2 error -> Some (Choice2Of2 error)
        | Choice1Of2 value -> rest value
    member this.Delay f = f
    member this.ReturnFrom f = Some f
    member this.Zero () = None
    member this.Combine (a, b) = 
        match a, b with
        | Some result, _ -> Some result
        | _, b -> b()
    member this.Run f =
        match f() with
        | Some result -> result

let choose = ChoiceBuilder()

let bad x = Choice2Of2 x
let good x = Choice1Of2 x

let result : Choice<int, string> = choose {
    if false then return 2
    let! a = bad "fail"
    if true then
        failwith "bind should be lazy"
        return! bad "crap"
    if false then return a
    failwith "combine should be lazy"
    return 5 }

    
printf "%A" result