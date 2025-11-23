using System.Collections.Generic;
using CircuitDiagram.Drawing;
using CircuitDiagram.Render.TypeDescription.Conditions;
using CircuitDiagram.TypeDescription.Conditions;
using CircuitDiagram.TypeDescriptionIO.Xml.Flatten;
using CircuitDiagram.TypeDescriptionIO.Xml.Render;

namespace CircuitDiagram.TypeDescriptionIO.Xml.Extensions.Definitions
{
    class XmlRectCommandWithDefinitions : XmlRectCommand
    {
        public new ConditionalCollection<double> Width { get; set; }

        public new ConditionalCollection<double> Height { get; set; }

        public override IEnumerable<Conditional<IRenderCommand>> Flatten(FlattenContext context)
        {
            foreach (var location in Location.Flatten(context))
            {
                foreach (var width in Width)
                {
                    foreach (var height in Height)
                    {
                        var conditions = ConditionTreeBuilder.And(new[]
                        {
                            location.Conditions,
                            width.Conditions,
                            height.Conditions,
                        });

                        double flatWidth = context.AutoRotate.Mirror ? height.Value : width.Value;
                        double flatHeight = context.AutoRotate.Mirror ? width.Value : height.Value;

                        var command = new Rectangle(
                            location.Value,
                            flatWidth,
                            flatHeight,
                            StrokeThickness,
                            Fill);

                        yield return new Conditional<IRenderCommand>(command, conditions);
                    }
                }
            }
        }
    }
}
