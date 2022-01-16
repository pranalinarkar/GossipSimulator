module GossipSimulator

open System
open Akka
open Akka.FSharp

open CommonUtil

[<EntryPoint>]
let main argv = 
    let mutable nodeCount = (argv.[0] |> int)
    let topology = argv.[1];
    let algorithm = argv.[2];
    let mutable debug = false
        
    let nodePerfectCube = getPerfectCube nodeCount
    let (layer, row, col) = if topology = "full" || topology = "line" then (1, 1, int (float nodePerfectCube ** 3.0)) else (nodePerfectCube, nodePerfectCube, nodePerfectCube)
    nodeCount <- layer * row * col

    printfn "Node count %d" nodeCount

    let systemName = "GossipSimulator"
    let actorName = "GossipActor_";
    let mutable actorsGrid = Unchecked.defaultof<ActorWrapper[,,]>
    let mutable counter = 0.0
    let stopwatch = System.Diagnostics.Stopwatch()

    let gossipSystem = System.create systemName <| Configuration.defaultConfig()
    setSystem gossipSystem


    let gossipActor (neighbors: (int*int*int)[]) (mailbox: Actor<Message>) = 
        let mutable rumorsCount = 0
        let mutable nonConvergedNeighbors: (int*int*int)[] = neighbors
        let mutable rumorReceived = false

        let rec loop() = actor {
            let! message = mailbox.Receive()

            if not(stopwatch.IsRunning) then stopwatch.Start()

            let (currentLayer, currentRow, currentCol) = getCurrentIndex mailbox.Self.Path.Name
            let actorWrapper = actorsGrid.[currentLayer, currentRow, currentCol]

            if not (actorWrapper.Converged) then 
                match message with
                | SendRumor ->
                    if rumorReceived then
                        getRandomNeighbor actorsGrid nonConvergedNeighbors currentLayer currentRow currentCol <! Rumor
                | Rumor ->
                    rumorReceived <- true
                    rumorsCount <- rumorsCount + 1
                    if (rumorsCount = 10) then
                        select ("akka://" + systemName + "/user/convergenceWatcher") gossipSystem <! Converged(mailbox.Self.Path.ToStringWithAddress())
                        actorWrapper.Converged <- true
                | RemoveNeighbor (z, x, y) ->
                    nonConvergedNeighbors <- Array.filter ((<>)(z, x, y)) nonConvergedNeighbors
                | Debug ->
                    let mutable debugMessage = "Actor " + (currentLayer.ToString()) + " " + (currentRow.ToString()) + " " + (currentCol.ToString()) + "\n"
                    debugMessage <- debugMessage + "Rumor received " + (rumorReceived.ToString()) + "\n"
                    debugMessage <- debugMessage + "Neighbors\n"
                    for neighbor in nonConvergedNeighbors do
                        debugMessage <- debugMessage + neighbor.ToString() + "\n"
                    printfn "%s" debugMessage
                | _ -> "Invalid message received" |> ignore

            return! loop();
        }
        loop()


    let pushSumActor (value: float) (neighbors: (int*int*int)[]) (mailbox: Actor<Message>) =
        let mutable s: double = value
        let mutable w: double = 1.0
        let mutable convergedCount: int = 0
        let mutable nonConvergedNeighbors = neighbors
        let mutable rumorReceivedd = false

        let rec loop() = actor {
            let! message = mailbox.Receive()

            if not(stopwatch.IsRunning) then stopwatch.Start()
            
            let (currentLayer, currentRow, currentCol) = getCurrentIndex mailbox.Self.Path.Name
            let currentActorWrapper = actorsGrid.[currentLayer, currentRow, currentCol]

            if not(currentActorWrapper.Converged) then
                match message with
                | SendRumor ->
                    if rumorReceivedd then
                        s <- s/2.0
                        w <- w/2.0
                        getRandomNeighbor actorsGrid nonConvergedNeighbors currentLayer currentRow currentCol <! PushSumMessage(s, w)
                | PushSumMessage (newS, newW) ->
                    rumorReceivedd <- true
                    if checkPushSumConvergedState (s, w) (newS, newW) then
                        convergedCount <- convergedCount + 1
                    else
                        convergedCount <- 0

                    if convergedCount = 3 then
                        currentActorWrapper.Converged <- true
                        select ("akka://" + systemName + "/user/convergenceWatcher") gossipSystem <! Converged(mailbox.Self.Path.ToStringWithAddress())
                    else
                        s <- s + newS
                        w <- w + newW
                | RemoveNeighbor (z, x, y) ->
                    nonConvergedNeighbors <- Array.filter ((<>)(z, x, y)) nonConvergedNeighbors
                | _ -> "Invalid message received" |> ignore

            return! loop()
        }
        loop()

    let gossipSupervisor =
        spawn gossipSystem "convergenceWatcher" (fun (mailbox: Actor<Message>) -> 
            let rec loop(convergedActorsCount: int) = actor {
                if (convergedActorsCount = nodeCount) then
                    stopwatch.Stop()
                    printfn "All actors converged successfully"
                    printfn "Time taken to converge the system %dms" stopwatch.ElapsedMilliseconds
                
                let! message = mailbox.Receive();
                let mutable newCount = convergedActorsCount

                match message with
                | Converged actorId ->
                    printfn "Actor with id %s converged %d" actorId convergedActorsCount
                    let (z, x, y) = getCurrentIndex actorId
                    for actor in mailbox.Context.GetChildren() do actor <! RemoveNeighbor((z, x, y))
                    newCount <- newCount + 1
                | CreateActors ->
                    match algorithm with
                    | "gossip" ->
                        actorsGrid <- Array3D.init layer row col (fun z x y ->
                            let neighbors = getNeighborIndices topology layer row col z x y 
                            {Actor=spawn mailbox.Context (actorName + z.ToString() + "_" + x.ToString() + "_" + y.ToString()) (gossipActor neighbors); Converged=false}
                        )
                    | "pushSum" ->
                        actorsGrid <- Array3D.init layer row col (fun z x y ->
                            counter <- counter+1.0
                            let neighbors = getNeighborIndices topology layer row col z x y
                            {Actor=spawn mailbox.Context (actorName + z.ToString() + "_" + x.ToString() + "_" + y.ToString()) (pushSumActor counter neighbors); Converged=false}
                        )
                    | _ -> 0 |> ignore
                    mailbox.Self <! BeginGossip
                | BeginGossip ->
                    let randomActor = getRandomActor actorsGrid topology (if topology = "full" || topology = "line" then nodeCount else nodePerfectCube)
                    if algorithm = "gossip" then
                        randomActor <! Rumor
                    else
                        randomActor <! PushSumMessage(0.0, 0.0)
                    mailbox.Self <! ScheduleActors
                | ScheduleActors ->
                    for i in 0..layer-1 do
                        for j in 0..row-1 do
                            for k in 0..col-1 do
                                gossipSystem.Scheduler.ScheduleTellRepeatedly(
                                    System.TimeSpan.FromMilliseconds(1000.0), System.TimeSpan.FromMilliseconds(100.0), actorsGrid.[i, j, k].Actor, SendRumor, actorsGrid.[i, j, k].Actor
                                )
                | Debug ->
                    for actor in mailbox.Context.GetChildren() do actor <! Debug
                | _ -> printfn "Invalid message received"
                return! loop(newCount)
            }
            loop (0))

    gossipSupervisor <! CreateActors

    Console.ReadLine() |> ignore

    0// return value for main function