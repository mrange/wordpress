open System
open System.Diagnostics
open System.Management
open System.Threading
open System.Threading.Tasks

open Hopac
open Hopac.Job.Infixes

let TimeIt (name : string) (iterations : int) (action : unit->'T) = 

    let pc = Environment.ProcessorCount

    let sw = Stopwatch ()
    sw.Start ()

    for x in 1..iterations do
        ignore <| action ()

    sw.Stop ()
    sw.ElapsedMilliseconds

module Fibonacci = 
    let rec hfib n = Job.delay <| fun () ->
      if n < 2L then
        Job.result n
      else
        hfib (n-2L) <*> hfib (n-1L) |>> fun (x, y) ->
        x + y

    let rec fib n = 
        if n < 2L then n
        else fib (n - 1L) + fib (n - 2L)

    let rec afib n = 
        async {
          if n < 2L then
            return n

          else
            let! n2a = afib (n-2L) |> Async.StartChild
            let! n1 = afib (n-1L)
            let! n2 = n2a
            return n2 + n1
        }

    let rec afib_seq n = 
        async {
          if n < 2L then
            return n

          else
            let! n1 = afib (n-1L)
            let! n2 = afib (n-2L)
            return n2 + n1
        }

    let rec tfib n =
      if n < 2L then n
      else
        let n2t = Task.Factory.StartNew (fun _ -> tfib (n-2L))
        let n1 = tfib (n-1L)
        n2t.Result + n1

module Mandelbrot = 
    
    let x2c (cutoff : int) (x : float) (y : float) (cx : float) (cy : float) = 
        let mutable i   = 0
        let mutable tx  = x
        let mutable ty  = y

        while i < cutoff && (tx*tx + ty*ty < 4.) do
            let tmpx = tx*tx - ty*ty + cx
            let tmpy = 2.*tx*ty + cy

            tx <- tmpx
            ty <- tmpy
            i <- i + 1

        i

    let lerp (t : float) (x1 : float) (x2 : float) = t*(x2 - x1) + x1

    let render_seq  (cutoff : int   ) 
                    (x1     : float ) 
                    (y1     : float ) 
                    (x2     : float ) 
                    (y2     : float ) 
                    (dimx   : int   ) 
                    (dimy   : int   ) 
                    (buffer : int[] ) =
        let minx = min x1 x2
        let miny = min y1 y2
        let maxx = max x1 x2
        let maxy = max y1 y2

        if buffer.Length <> dimx*dimy then
            failwith "Wrong dimension of buffer; is %d but expected to be %d" buffer.Length (dimx*dimy)

        for iy in 0..(dimy - 1) do
            let offset = iy*dimx
            let y = lerp (float iy / float dimy) miny maxy
            for ix in 0..(dimx - 1) do
                let x = lerp (float ix / float dimx) minx maxx

                buffer.[ix + offset] <- x2c cutoff x y x y

    let render_para (cutoff : int   ) 
                    (x1     : float ) 
                    (y1     : float ) 
                    (x2     : float ) 
                    (y2     : float ) 
                    (dimx   : int   ) 
                    (dimy   : int   ) 
                    (buffer : int[] ) =
        let minx = min x1 x2
        let miny = min y1 y2
        let maxx = max x1 x2
        let maxy = max y1 y2

        if buffer.Length <> dimx*dimy then
            failwith "Wrong dimension of buffer; is %d but expected to be %d" buffer.Length (dimx*dimy)

        let render_row iy = 
            let offset = iy*dimx
            let y = lerp (float iy / float dimy) miny maxy
            for ix in 0..(dimx - 1) do
                let x = lerp (float ix / float dimx) minx maxx

                buffer.[ix + offset] <- x2c cutoff x y x y

        ignore <| Parallel.For (0, dimy, render_row)

    let render_task (cutoff : int   ) 
                    (x1     : float ) 
                    (y1     : float ) 
                    (x2     : float ) 
                    (y2     : float ) 
                    (dimx   : int   ) 
                    (dimy   : int   ) 
                    (buffer : int[] ) =
        let minx = min x1 x2
        let miny = min y1 y2
        let maxx = max x1 x2
        let maxy = max y1 y2

        if buffer.Length <> dimx*dimy then
            failwith "Wrong dimension of buffer; is %d but expected to be %d" buffer.Length (dimx*dimy)

        let render_row iy = 
            let offset = iy*dimx
            let y = lerp (float iy / float dimy) miny maxy
            for ix in 0..(dimx - 1) do
                let x = lerp (float ix / float dimx) minx maxx

                buffer.[ix + offset] <- x2c cutoff x y x y

        let tasks = [|for iy in 0..(dimy-1) -> Task.Run (fun () -> render_row iy)|]

        Task.WaitAll tasks

    let render_limited_task (cutoff : int   ) 
                            (x1     : float ) 
                            (y1     : float ) 
                            (x2     : float ) 
                            (y2     : float ) 
                            (dimx   : int   ) 
                            (dimy   : int   ) 
                            (buffer : int[] ) =
        let minx = min x1 x2
        let miny = min y1 y2
        let maxx = max x1 x2
        let maxy = max y1 y2

        if buffer.Length <> dimx*dimy then
            failwith "Wrong dimension of buffer; is %d but expected to be %d" buffer.Length (dimx*dimy)

        let gy = ref -1
        
        let nextRow () = 
            let y = Interlocked.Increment gy

            if y < dimy then
                y
            else 
                -1

        let render_row iy = 
            let offset = iy*dimx
            let y = lerp (float iy / float dimy) miny maxy
            for ix in 0..(dimx - 1) do
                let x = lerp (float ix / float dimx) minx maxx

                buffer.[ix + offset] <- x2c cutoff x y x y

        let taskCount   = 2*Environment.ProcessorCount
        let action ()   = 
            let mutable y = nextRow ()
            while y > 0 do
                render_row y
                y <- nextRow ()

        let tasks       = [|for iy in 0..(taskCount - 1) -> Task.Run action|]

        Task.WaitAll tasks

    let render_async    (cutoff : int   ) 
                        (x1     : float ) 
                        (y1     : float ) 
                        (x2     : float ) 
                        (y2     : float ) 
                        (dimx   : int   ) 
                        (dimy   : int   ) 
                        (buffer : int[] ) =
        let minx = min x1 x2
        let miny = min y1 y2
        let maxx = max x1 x2
        let maxy = max y1 y2

        if buffer.Length <> dimx*dimy then
            failwith "Wrong dimension of buffer; is %d but expected to be %d" buffer.Length (dimx*dimy)

        let render_row iy = 
            async {
                let offset = iy*dimx
                let y = lerp (float iy / float dimy) miny maxy
                for ix in 0..(dimx - 1) do
                    let x = lerp (float ix / float dimx) minx maxx

                    buffer.[ix + offset] <- x2c cutoff x y x y
            }

        let render = 
            async {
                let! _ = Async.Parallel [|for iy in 0..(dimy-1) -> render_row iy|]
                return ()
                }

        Async.RunSynchronously render

    let render_hopac    (cutoff : int   ) 
                        (x1     : float ) 
                        (y1     : float ) 
                        (x2     : float ) 
                        (y2     : float ) 
                        (dimx   : int   ) 
                        (dimy   : int   ) 
                        (buffer : int[] ) =
        let minx = min x1 x2
        let miny = min y1 y2
        let maxx = max x1 x2
        let maxy = max y1 y2

        if buffer.Length <> dimx*dimy then
            failwith "Wrong dimension of buffer; is %d but expected to be %d" buffer.Length (dimx*dimy)

        let render_row iy = 
            Job.delay <| fun () ->
                let offset = iy*dimx
                let y = lerp (float iy / float dimy) miny maxy
                for ix in 0..(dimx - 1) do
                    let x = lerp (float ix / float dimx) minx maxx

                    buffer.[ix + offset] <- x2c cutoff x y x y

                Job.result ()
        let render = Job.conIgnore (seq { for iy in 0..(dimy-1) -> render_row iy })

        run render

    let render_hopac2   (cutoff : int   ) 
                        (x1     : float ) 
                        (y1     : float ) 
                        (x2     : float ) 
                        (y2     : float ) 
                        (dimx   : int   ) 
                        (dimy   : int   ) 
                        (buffer : int[] ) =
        let minx = min x1 x2
        let miny = min y1 y2
        let maxx = max x1 x2
        let maxy = max y1 y2

        if buffer.Length <> dimx*dimy then
            failwith "Wrong dimension of buffer; is %d but expected to be %d" buffer.Length (dimx*dimy)

        let render_point ix iy = 
            Job.delay <| fun () ->
                let y = lerp (float iy / float dimy) miny maxy
                let x = lerp (float ix / float dimx) minx maxx

                buffer.[ix + iy*dimx] <- x2c cutoff x y x y

                Job.result ()
        let render = Job.conIgnore (seq { for iy in 0..(dimy-1) do for ix in 0..(dimx-1) -> render_point ix iy })

        run render


[<EntryPoint>]
let main argv = 
    let proc                = Process.GetCurrentProcess ()
    let multiCoreAffinity   = proc.ProcessorAffinity    
    let singleCoreAffinity  = nativeint 1

    let coreCount           =
        let rec count bits = 
            if bits = 0 then 0
            elif (bits &&& 1) = 1 then 1 + count (bits >>> 1)
            else count (bits >>> 1)
        count <| int multiCoreAffinity

    printfn "Number of cores available to process: %A" coreCount

    let fibn = 24L


    let mandelbrot_dimx     = 1024
    let mandelbrot_dimy     = 1024
    let mandelbrot_buffer   = Array.zeroCreate<int> (mandelbrot_dimx*mandelbrot_dimy)

    let render_mandelbrot_inplace dimx dimy buffer render = 
        let cutoff  = 1000
        let cx      = 0.001643721971153
        let cy      = 0.822467633298876
        let halfside= 0.1
        let x1      = cx - halfside
        let y1      = cy - halfside
        let x2      = cx + halfside
        let y2      = cy + halfside

        render cutoff x1 y1 x2 y2 dimx dimy buffer

    let inline render_mandelbrot render = 
        render_mandelbrot_inplace mandelbrot_dimx mandelbrot_dimy mandelbrot_buffer render

        0L

    let inline render_mandelbrot2 render = 
        let buffer = Array.zeroCreate<int> (mandelbrot_dimx*mandelbrot_dimy)
        render_mandelbrot_inplace mandelbrot_dimx mandelbrot_dimy buffer render

        buffer |> Array.sumBy int64



    let testSuites =
        [
(*
            "Sequential Fibonacci"  , 4000  , fun () -> Fibonacci.fib fibn
            "Task Fibonacci"        , 100   , fun () -> Fibonacci.tfib fibn
            "Hopac Fibonacci"       , 100   , fun () -> let j = Fibonacci.hfib fibn
                                                        run j
            "Async Fibonacci"       , 1     , fun () -> let t = Fibonacci.afib fibn |> Async.StartAsTask
                                                        t.Result
            "Async Fibonacci (seq)" , 1     , fun () -> let t = Fibonacci.afib_seq fibn |> Async.StartAsTask
                                                        t.Result
*)
            "Sequential Mandelbrot"         , 1     , fun () -> render_mandelbrot Mandelbrot.render_seq
//            "Parallel Mandelbrot"           , 1     , fun () -> render_mandelbrot Mandelbrot.render_para
//            "Task Mandelbrot"               , 1     , fun () -> render_mandelbrot Mandelbrot.render_task
            "Task (coarse) Mandelbrot"      , 1     , fun () -> render_mandelbrot Mandelbrot.render_limited_task
//            "Async Mandelbrot"              , 1     , fun () -> render_mandelbrot Mandelbrot.render_async
//            "Hopac Mandelbrot"              , 1     , fun () -> render_mandelbrot Mandelbrot.render_hopac
//            "Hopac (granular) Mandelbrot"   , 1     , fun () -> render_mandelbrot Mandelbrot.render_hopac2
        ]

    let runTestSuites () = 
        for name, iter, action in testSuites do
            let r = action ()
            printfn "Running test suite: %A (%A)" name r
            let ms = TimeIt name iter action
            let ams = Math.Round (decimal ms / decimal iter, 2)
            printfn "Completed test suite: %A in %f ms" name ams

    printfn "Running test suite using all available cores"
    runTestSuites ()

    proc.ProcessorAffinity <- singleCoreAffinity
    // Give OS sometime to react
    Thread.Sleep 500

    printfn "Running test suite using one core"
    runTestSuites ()

    0
