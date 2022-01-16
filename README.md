# GossipSimulatorReport

## Group Members:
1. Pranali Suhas Narkar
2. Manish

## What is working

We have successfully simulated follwoing algorithms and topologies using FSharp Akka.net framework

Protocols:
1. Gossip
2. Push-Sum

Topologies:
1. Full Network
2. Line Network
3. 3D Grid
4. Imperfect 3D grid.

## Largest network
Following are the different number of actors that we were able to run with for different algorithms and topologies

1. Gossip <br />
  1.1 Full network: 10648<br />
  1.2 Line network: 1000<br />
  1.3 3D: 10648<br />
  1.3 Imperfect 3D: 10648<br />
  
2. Push-Sum <br />
  1.1 Full network: 5832<br />
  1.2 Line network: 1000<br />
  1.3 3D: 1000<br />
  1.3 Imperfect 3D: 5832<br />
  
  
## How to run?

We have provided both the F# source code and standalone executable file. The program can be run by either importing the source code in Visual Studio IDE (not VS Code) or by directly running the standalone exe file as shown below.

GossipSimulator 100 full gossip

Param 1: Number of nodes (e.g. 100)<br />
Param 2: Topology (e.g. full, line, 3D, imp3D)<br />
Param 3: Algorithm (e.g. gossip, pushSum)<br />
  
