Circuit Diagram
===============

[![Build status](https://ci.appveyor.com/api/projects/status/8xt15xqjat9ime9f/branch/master?svg=true)](https://ci.appveyor.com/project/CircuitDiagram/circuitdiagram/branch/master)

Design Circuit Diagrams with support for custom user-defined components.

This repository contains the command-line application and utilities for working
with circuit diagrams and custom components. For the graphical editor please
visit [www.circuit-diagram.org](https://www.circuit-diagram.org/).

## Downloads

Compiled binaries and an installer for Windows are available at [www.circuit-diagram.org/downloads](https://www.circuit-diagram.org/downloads).

## Custom Components

Circuit Diagram includes most commonly used components but you can download more from
the [Circuit Diagram Website](https://www.circuit-diagram.org/components).

For creating your own custom components, see
[Components Introduction](https://www.circuit-diagram.org/docs/components/introduction).

## Building

Open *CircuitDiagram/CircuitDiagram.sln* in Visual Studio. The dependencies should download automatically.

## Development UI

A .NET MAUI-based graphical interface is available in the `CircuitDiagram.UI` project. This tool allows for testing custom components and visualizing circuit diagrams during development.

To run the UI:

1. Ensure you have the .NET MAUI workload installed (`dotnet workload install maui`).
2. Navigate to the `CircuitDiagram/CircuitDiagram.UI` directory.
3. Run the project using `dotnet run -f net9.0-maccatalyst` (or your preferred platform).

For more details, see the [CircuitDiagram.UI README](CircuitDiagram/CircuitDiagram.UI/README.md).

## Issues

Please submit all issues, bugs and feature requests using the [GitHub issues tracker](https://github.com/circuitdiagram/circuitdiagram/issues).
