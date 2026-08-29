FeatureScript 1977;
import(path : "onshape/std/geometry.fs", version : "1540.0");
CHAIN_LINK::import(path : "aed5053b11da8477a6cb2a0d", version : "79707921b23c639225c9d509"); //configurable standard link (alternates interior/exterior)
GOBILDA_PLASTIC::import(path : "cfd248612a76fddb542d0788", version : "b16c7b7bd766d0c0b7735392"); //specifically for goBILDA's plastic chain
icon::import(path : "c723c47a90d5d43aa6e63d99", version : "523e3696d2e9caec7e36575d");
ImageNamespace::import(path : "22d5bf8c02de0e99e1153753", version : "b223e9dc1cef377466a3039d");
SPROCKET::import(path : "bdafd79daaa9dfcca06e592d", version : "9530cc945e619f926b477800");
PULLEY::import(path : "8cb7635b79c9a5c8062e91b8", version : "f99358aab24b48c61cbaa582");

/*

 */
export enum genType
{
    annotation { "Name" : "Chain" }
    CHAIN,
    annotation { "Name" : "Belt" }
    BELT,
}

export enum PathType
{
    annotation { "Name" : "Select points" }
    POINTS,
    annotation { "Name" : "Custom path" }
    EDGES,
}


export enum LinkType
{
    annotation { "Name" : "Standard links" }
    STANDARD,
    annotation { "Name" : "Custom links" }
    CUSTOM,
}

export enum ErrorType
{
    annotation { "Name" : "Don't cover gap" } //generate links spaced by pitch, do nothing about a gap at the end
    IGNORE,
    //annotation { "Name" : "Extend links" } deprecated, now handled with adding&connecting links
    //EXTEND,
    annotation { "Name" : "Add link and compress" } //correct for a gap at the end by visually shortening pitch slightly, adding one link (IRL chain would sag a bit)
    CONTRACT,
}

//selection table for all standard chain types, with the lookup table path corresponding to a configured chain link
export const ProfileTable = {
        "name" : "standard",
        "displayName" : "Standard",
        "default" : "ANSI",
        "entries" : {
            "ANSI" : {
                "name" : "type",
                "displayName" : "Type",
                "entries" : {
                    "#25-1" : "ansi25",
                    "#35-1" : "ansi35",
                    "#40-1" : "ansi40",
                    "#41-1" : "ansi41",
                    "#50-1" : "ansi50",
                    "#60-1" : "ansi60",
                    "#80-1" : "ansi80",
                    "#100-1" : "ansi100",
                    "#120-1" : "ansi120",
                    "#140-1" : "ansi140",
                    "#160-1" : "ansi160",
                    "#180-1" : "ansi180",
                    "#200-1" : "ansi200",
                    "#240-1" : "ansi240",
                }
            },
            "ISO/DIN" : {
                "name" : "type",
                "displayName" : "Type",
                "entries" : {
                    "04C-1" : "ISO_04C_1",
                    "06C-1" : "ISO_06C_1",
                    "08A-1" : "ISO_08A_1",
                    "10A-1" : "ISO_10A_1",
                    "12A-1" : "ISO_12A_1",
                    "16A-1" : "ISO_16A_1",
                    "20A-1" : "ISO_20A_1",
                    "24A-1" : "ISO_24A_1",
                    "28A-1" : "ISO_28A_1",
                    "32A-1" : "ISO_32A_1",
                    "36A-1" : "ISO_36A_1",
                    "40A-1" : "ISO_40A_1",
                    "48A-1" : "ISO_48A_1",
                }
            },
            "FTC" : {
                "name" : "type",
                "displayName" : "Type",
                "entries" : {
                    "REV #25-1" : "ansi25",
                    "goBILDA Steel Chain" : "goBILDA_Steel",
                    "ģoBILDA Plastic Chain" : "GB_Plastic",
                }
            },
        }
    };

const TEETH_BOUNDS =
{
            (unitless) : [4, 10, 500] //supports sprockets 4-500 teeth, defaults to 10
        } as IntegerBoundSpec;
const PITCH_BOUNDS =
{(millimeter) : [1, 5, 10]} as LengthBoundSpec;
const WIDTH_BOUNDS =
{
            (millimeter) : [1, 9, 100]
        } as LengthBoundSpec;
const TENSIONER_BOUNDS =
{
            (millimeter) : [0, 12, 10000]
        } as LengthBoundSpec;
annotation { "Feature Type Name" : "Chain Gen",
        "Feature Name Template" : "#gentype #teeth",
        "Icon" : icon::BLOB_DATA,
        "Feature Type Description" : "Creates a composite part with patterned chain links",
        "Description Image" : ImageNamespace::BLOB_DATA } //imported screenshot of sprocket around gear
export const myFeature = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Plane", "Filter" : (GeometryType.PLANE && EntityType.FACE) || BodyType.MATE_CONNECTOR, "MaxNumberOfPicks" : 1 }
        definition.plane is Query;

        annotation { "Name" : "Generator" }
        definition.genType is genType;

        annotation { "Group Name" : "Path Inputs", "Collapsed By Default" : true } //path inputs determine the chain's path around sprockets
        {
            annotation { "Name" : "Path Type", "UIHint" : "HORIZONTAL_ENUM" }
            definition.pathType is PathType;
            if (definition.pathType == PathType.POINTS) //user inputs sprocket locations, FS will generate path later
            {
                annotation { "Name" : "Sprockets/pulleys", "Item name" : "Sprocket/Pulley ", "UIHint" : [UIHint.DISPLAY_SHORT], "UiHint" : [UIHint.FOCUS_INNER_QUERY] }
                definition.sprockets is array;
                for (var sprocket in definition.sprockets)
                {
                    annotation { "Name" : "Location", "Filter" : EntityType.VERTEX, "MaxNumberOfPicks" : 1 }
                    sprocket.location is Query; //sketch vertex for the center of a sprocket
                    annotation { "Name" : "Tensioner" }
                    sprocket.tensioner is boolean;

                    if (sprocket.tensioner)
                    {
                        annotation { "Name" : "Diameter" }
                        isLength(sprocket.tensionerDiameter, TENSIONER_BOUNDS);
                    }
                    else
                    {
                        annotation { "Name" : "Teeth", "UIHint" : [UIHint.MATCH_LAST_ARRAY_ITEM] }
                        isInteger(sprocket.teeth, TEETH_BOUNDS); //number of teeth for given sprocket
                    }
                    annotation { "Name" : "Change Wrap Direction", "UIHint" : [UIHint.DISPLAY_SHORT, UIHint.OPPOSITE_DIRECTION_CIRCULAR, UIHint.MATCH_LAST_ARRAY_ITEM] }
                    sprocket.clockwise is boolean; //change wrap direction clockwise/counterclockwise
                }
                annotation { "Name" : "Create Mate Connectors" }
                definition.mates is boolean;

                annotation { "Name" : "Create Sprockets/Pulleys" }
                definition.makeElements is boolean;
            }
            else //if (definition.pathType == PathType.EDGES) //user has sketched the chain path, inputs edges to pattern along
            {
                annotation { "Name" : "Path", "Filter" : BodyType.WIRE || EntityType.EDGE }
                definition.path is Query;
                annotation { "Name" : "Starting point", "Filter" : EntityType.VERTEX, "MaxNumberOfPicks" : 1 }
                definition.start is Query;
                if (definition.genType == genType.BELT)
                {
                    annotation { "Name" : "Flip Teeth" }
                definition.flipTeeth is boolean;
                }

            }
        }
        if (definition.genType == genType.CHAIN)
        {
            annotation { "Group Name" : "link inputs", "Collapsed By Default" : true }
            {
                annotation { "Name" : "GenType", "UIHint" : "HORIZONTAL_ENUM" }
                definition.linkType is LinkType;
                annotation { "Name" : "Sweep profile" } //uses maximum chain profile and sweeps intersection path
                definition.sweep is boolean;

                if (!definition.sweep)
                {
                    annotation { "Name" : "How to cover up errors" }
                    definition.errorType is ErrorType;
                }

                if (definition.linkType == LinkType.CUSTOM) //user inputs custom link geometry from within the part studio
                {
                    annotation { "Name" : "Pitch" } //Pitch = linear distance between chain links, determines spacing when patterning
                    isLength(definition.pitch, LENGTH_BOUNDS);
                    if (definition.sweep)
                    {
                        annotation { "Name" : "Width" }
                        isLength(definition.width, WIDTH_BOUNDS);
                        annotation { "Name" : "Height" }
                        isLength(definition.height, LENGTH_BOUNDS);
                    }
                    else
                    {

                        annotation { "Name" : "Max Roller Diameter" }
                        isLength(definition.RollerD, LENGTH_BOUNDS);
                        annotation { "Name" : "Unique links", "Item name" : "Link" }
                        definition.links is array;
                        for (var link in definition.links)
                        {
                            annotation { "Name" : "Link body", "Filter" : EntityType.BODY, "MaxNumberOfPicks" : 1 }
                            link.body is Query;
                            annotation { "Name" : "Link mate connector", "Filter" : BodyType.MATE_CONNECTOR, "MaxNumberOfPicks" : 1, "UIHint" : "DISPLAY_SHORT" }
                            link.mate is Query;
                        }
                    }
                }
                else if (definition.linkType == LinkType.STANDARD) //
                {
                    annotation { "Name" : "Profile", "Lookup Table" : ProfileTable, "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
                    definition.profile is LookupTablePath;
                }
            }
        }
        else
        {
            annotation { "Name" : "Pitch", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isLength(definition.beltPitch, PITCH_BOUNDS);

            annotation { "Name" : "Width", "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isLength(definition.beltWidth, WIDTH_BOUNDS);

            annotation { "Name" : "Generate with teeth" }
            definition.beltTeeth is boolean;
        }

        annotation { "Name" : "Exclude from BOM" }
        definition.excludefromBOM is boolean;
    }
    {
        var isChain = definition.genType == genType.CHAIN;
        //context and pitch will be used later for link instantiation and calculations for link placement
        var chainLinkContext;
        if (definition.linkType == LinkType.STANDARD)
        {
            chainLinkContext = CHAIN_LINK::build({ "Standard" : CHAIN_LINK::Standard_conf[getLookupTable(ProfileTable, definition.profile)] });
        }

        var pitch;
        var Dr;
        var thickness;
        var sprocketInstantiator = newInstantiator(id);
        var pulleyInstantiator;
        if (isChain)
        {
            pitch = definition.linkType == LinkType.STANDARD ? getVariable(chainLinkContext, "P") : definition.pitch;
            setFeatureComputedParameter(context, id, { "name" : "gentype", "value" : "Chain Generator - " });
            if (definition.makeElements)
            {
                Dr = getVariable(chainLinkContext, "D");
                thickness = getVariable(chainLinkContext, "W");
                print(Dr);
            }
        }
        else
        {
            pitch = definition.beltPitch;
            setFeatureComputedParameter(context, id, { "name" : "gentype", "value" : "Belt Generator - " });
        }
        var localPlane = evPlane(context, { "face" : definition.plane });
        var edgePlane = plane(project(localPlane, vector(0, 0, 0) * meter), localPlane.normal, localPlane.x); //ensures that origin for local plane is useable
        var edges;
        var startPoint2d;
        var startPoint3d;
        var matelocs = makeArray(size(definition.sprockets));
        if (definition.pathType == PathType.EDGES) //sketched path, no sprocket inputs
        {
            edges = definition.path;

            startPoint3d = evVertexPoint(context, { "vertex" : definition.start });
            startPoint2d = worldToPlane(edgePlane, startPoint3d);
        }
        else if (definition.pathType == PathType.POINTS && size(definition.sprockets) != 1) //point inputs for sprocket positions
        {
            //convert sprocket location inputs from 3d space to locations on input plane
            var sprocketLocations2d = makeArray(size(definition.sprockets));
            for (var i = 0; i < size(sprocketLocations2d); i += 1)
            {
                sprocketLocations2d[i] = worldToPlane(edgePlane, evVertexPoint(context, { "vertex" : definition.sprockets[i].location }));
                matelocs[i] = coordSystem(planeToWorld(edgePlane, sprocketLocations2d[i]), edgePlane.x, edgePlane.normal);
            }
            var sketch1 = newSketchOnPlane(context, id + "sketch1", { "sketchPlane" : edgePlane }); //will be used to sketch the chain path
            var MaxD = 2 * (0.285 * definition.beltPitch + 0.33 * millimeter);

            if (definition.genType == genType.CHAIN)
            {
                MaxD = (definition.linkType == LinkType.STANDARD ? getVariable(chainLinkContext, "H") : definition.RollerD);
                if ((definition.linkType == LinkType.CUSTOM) && (definition.sweep))
                {
                    MaxD = definition.height;
                }
            }
            for (var i = 0; i < size(definition.sprockets); i += 1)
            {
                var prevIndex = (i == 0 ? size(definition.sprockets) : i) - 1; //the previous sprocket index (wraps to last for i=0)
                var nextIndex = (i + 1) % size(definition.sprockets); //the next sprocket index

                var sprocket = definition.sprockets[i];
                var prevSprocket = definition.sprockets[prevIndex];
                var nextSprocket = definition.sprockets[nextIndex];

                var current = sprocketLocations2d[i];
                var prev = sprocketLocations2d[prevIndex];
                var next = sprocketLocations2d[nextIndex];

                var distToPrev = evDistance(context, {
                            "side0" : planeToWorld(edgePlane, current),
                            "side1" : planeToWorld(edgePlane, prev)
                        }).distance;
                var distToNext = evDistance(context, {
                            "side0" : planeToWorld(edgePlane, current),
                            "side1" : planeToWorld(edgePlane, next)
                        }).distance;

                var radius;
                var prevRadius;
                var nextRadius;

                radius = sprocket.tensioner ? (sprocket.tensionerDiameter + MaxD) / 2 : (isChain ? pitch / (sin(180 * degree / sprocket.teeth)) / 2 : (pitch * sprocket.teeth / PI - (.191 * pitch + .1885 * millimeter)) / 2);
                println(radius);
                prevRadius = prevSprocket.tensioner ? (prevSprocket.tensionerDiameter + MaxD) / 2 : (isChain ? pitch / (sin(180 * degree / prevSprocket.teeth)) / 2 : (pitch * prevSprocket.teeth / PI - (.191 * pitch + .1885 * millimeter)) / 2);
                nextRadius = nextSprocket.tensioner ? (nextSprocket.tensionerDiameter + MaxD) / 2 : (isChain ? pitch / (sin(180 * degree / nextSprocket.teeth)) / 2 : (pitch * nextSprocket.teeth / PI - (.191 * pitch + .1885 * millimeter)) / 2);

                var sameAsPrev = prevSprocket.clockwise == sprocket.clockwise;
                var prevEndOffset = acos((prevRadius + (sameAsPrev ? -1 : 1) * radius) / distToPrev);
                var currStartOffset = sameAsPrev ? 180 * degree - prevEndOffset : prevEndOffset;

                var sameAsNext = sprocket.clockwise == nextSprocket.clockwise;
                var currEndOffset = acos((radius + (sameAsNext ? -1 : 1) * nextRadius) / distToNext);
                var nextStartOffset = sameAsNext ? 180 * degree - currEndOffset : currEndOffset;

                var startAngle = atan2((prev - current)[1], (prev - current)[0]) + currStartOffset * (sprocket.clockwise ? 1 : -1);
                var endAngle = atan2((next - current)[1], (next - current)[0]) + currEndOffset * (sprocket.clockwise ? -1 : 1);
                var midAngle = startAngle + (sprocket.clockwise ? .1 : -.1) * degree;
                var nextStartAngle = atan2((current - next)[1], (current - next)[0]) + nextStartOffset * (nextSprocket.clockwise ? 1 : -1);

                var point00 = arcPoint(current, radius, startAngle);
                var point01 = arcPoint(current, radius, midAngle);
                var point02 = arcPoint(current, radius, endAngle);
                var point10 = arcPoint(next, nextRadius, nextStartAngle);
                skArc(sketch1, "arc" ~ i, { "start" : point00, "mid" : point01, "end" : point02 });
                skLineSegment(sketch1, "line" ~ i, { "start" : point02, "end" : point10 });

                if (i == 0)
                {
                    startPoint2d = point00;
                    startPoint3d = planeToWorld(edgePlane, startPoint2d);
                }
                if (definition.makeElements)
                {
                    if (isChain)
                    {
                        addInstance(sprocketInstantiator, SPROCKET::build, { "configuration" : {
                                        "P" : pitch,
                                        "Dr" : Dr,
                                        "N" : sprocket.teeth
                                    },
                                    "transform" : transform(plane(WORLD_COORD_SYSTEM), plane(evVertexPoint(context, { "vertex" : definition.sprockets[i].location }), localPlane.normal))
                                });
                    }
                    else
                    {
                        addInstance(sprocketInstantiator, PULLEY::build, { "configuration" : {
                                        "P" : pitch,
                                        "W" : definition.beltWidth,
                                        "N" : sprocket.teeth
                                    },
                                    "transform" : transform(plane(WORLD_COORD_SYSTEM), plane(evVertexPoint(context, { "vertex" : definition.sprockets[i].location }), localPlane.normal))
                                });
                    }
                }
            }

            skSolve(sketch1);

            edges = qBodyType(qCreatedBy(id + "sketch1", EntityType.EDGE), BodyType.WIRE);
        }
        else if (definition.makeElements)
        {
            if (isChain)
            {
                addInstance(sprocketInstantiator, SPROCKET::build, { "configuration" : {
                                "P" : pitch,
                                "Dr" : Dr,
                                "N" : definition.sprockets[0].teeth
                            },
                            "transform" : transform(plane(WORLD_COORD_SYSTEM), plane(evVertexPoint(context, { "vertex" : definition.sprockets[0].location }), localPlane.normal))
                        });
                setFeatureComputedParameter(context, id, { "name" : "gentype", "value" : "Sprocket Blank- " });


            }
            else
            {
                addInstance(sprocketInstantiator, PULLEY::build, { "configuration" : {
                                "P" : pitch,
                                "W" : definition.beltWidth,
                                "N" : definition.sprockets[0].teeth
                            },
                            "transform" : transform(plane(WORLD_COORD_SYSTEM), plane(evVertexPoint(context, { "vertex" : definition.sprockets[0].location }), localPlane.normal))
                        });
                setFeatureComputedParameter(context, id, { "name" : "gentype", "value" : "HTD" ~ round(pitch / millimeter) ~ " Pulley Blank - " });

            }
            setFeatureComputedParameter(context, id, { "name" : "teeth", "value" : toString(definition.sprockets[0].teeth) ~ " teeth" });
        }
        if (definition.makeElements)
        {
            instantiate(context, sprocketInstantiator);
        }
        if (size(definition.sprockets) > 1 || definition.pathType == PathType.EDGES)
        {

            var numPoints = floor(evLength(context, { "entities" : edges }) / pitch + 0.0001);
            var curves = qContainsPoint(edges, startPoint3d);
            var parameter = evDistance(context, { "side0" : startPoint3d, "side1" : curves }).sides[1].parameter;
            var normal = evEdgeTangentLine(context, { "edge" : curves, "parameter" : round(parameter) });

            if (!isChain)
            {
                var beltPlane = plane(startPoint3d, normal.direction * (definition.flipTeeth ? -1 : 1), edgePlane.normal);
                var BeltBase = newSketchOnPlane(context, id + "beltBaseSketch", {
                        "sketchPlane" : beltPlane
                    });

                var tdiam = .635 * pitch - .125 * millimeter;
                var tdepth = .4245 * pitch - .0294 * millimeter;
                var bd = 0.2554 * pitch + .4648 * millimeter;
                var beltCircleCenter = planeToWorld(beltPlane, vector(0 * millimeter, tdiam / 2 - tdepth));
                skRectangle(BeltBase, "rectangle1", {
                            "firstCorner" : vector(-1 * definition.beltWidth / 2, (definition.beltTeeth ? 0 * millimeter : -tdepth)),
                            "secondCorner" : vector(definition.beltWidth / 2, bd)
                        });
                skSolve(BeltBase);
                opSweep(context, id + "sweep1", {
                            "profiles" : qSketchRegion(id + "beltBaseSketch"),
                            "path" : edges
                        });
                setProperty(context, {
                            "entities" : qCreatedBy(id + "sweep1", EntityType.BODY),
                            "propertyType" : PropertyType.NAME,
                            "value" : toString(numPoints) ~ "T  HTD" ~ toString(definition.beltPitch / millimeter) ~ " Belt"
                        });
                setProperty(context, {
                            "entities" : qCreatedBy(id + "sweep1", EntityType.BODY),
                            "propertyType" : PropertyType.APPEARANCE,
                            "value" : color(0.3, 0.3, 0.3)
                        });
                if (definition.pathType == PathType.POINTS && definition.mates)
                {
                    for (var i = 0; i < size(definition.sprockets); i += 1)
                    {
                        opMateConnector(context, id + ("mateConnector" ~ i), {
                                    "coordSystem" : matelocs[i],
                                    "owner" : qCreatedBy(id + "sweep1", EntityType.BODY)
                                });
                    }
                }
                if (definition.beltTeeth)
                {
                    var toothPlane = plane(startPoint3d, edgePlane.normal, edgePlane.x);
                    var toothSketch = newSketchOnPlane(context, id + "ToothSketch", {
                            "sketchPlane" : toothPlane
                        });
                    var flipper = 1;
                    if (definition.pathType == PathType.POINTS)
                    {
                        flipper = (definition.sprockets[0].clockwise ? -1 : 1);
                    }
                    else
                    {
                        flipper = (definition.flipTeeth ? -1 : 1);
                    }
                    skCircle(toothSketch, "circle1", {
                                "center" : worldToPlane(toothPlane, beltCircleCenter),
                                "radius" : tdiam / 2
                            });
                    skSolve(toothSketch);
                    opExtrude(context, id + "extrude1", {
                                "entities" : qSketchRegion(id + "ToothSketch"),
                                "direction" : edgePlane.normal,
                                "endBound" : BoundingType.BLIND,
                                "endDepth" : definition.beltWidth / 2,
                                "startBound" : BoundingType.BLIND,
                                "startDepth" : definition.beltWidth / 2
                            });

                    curvePattern(context, id + "curvePattern1", {
                                "patternType" : PatternType.PART,
                                "entities" : qCreatedBy(id + "extrude1", EntityType.BODY),
                                "edges" : edges,
                                "instanceCount" : numPoints
                            });
                    opBoolean(context, id + "boolean1", {
                                "tools" : qUnion([qCreatedBy(id + "sweep1", EntityType.BODY), qCreatedBy(id + "extrude1", EntityType.BODY), qCreatedBy(id + "curvePattern1", EntityType.BODY)]),
                                "operationType" : BooleanOperationType.UNION
                            });
                }

            }
            else
            {
                if (definition.sweep)
                {
                    var pinWidth = definition.linkType == LinkType.STANDARD ? getVariable(chainLinkContext, "A") : definition.width;
                    var height = definition.linkType == LinkType.STANDARD ? getVariable(chainLinkContext, "H") : definition.height;
                    var pinDiameter = definition.linkType == LinkType.STANDARD ? getVariable(chainLinkContext, "E") : definition.height / 4;
                    var thickness = definition.linkType == LinkType.STANDARD ? getVariable(chainLinkContext, "T") : definition.width / 6;
                    var innerWidth = definition.linkType == LinkType.STANDARD ? getVariable(chainLinkContext, "W") : definition.width / 4;
                    var width = (innerWidth + 4 * thickness);

                    var sketch2 = newSketchOnPlane(context, id + "sketch2", {
                            "sketchPlane" : plane(startPoint3d, normal.direction, edgePlane.normal)
                        });
                    skRectangle(sketch2, "rectangle1", {
                                "firstCorner" : vector(-pinWidth / 2, -pinDiameter / 2),
                                "secondCorner" : vector(pinWidth / 2, pinDiameter / 2)
                            });
                    skRectangle(sketch2, "rectangle2", {
                                "firstCorner" : vector(-width / 2, -height / 2),
                                "secondCorner" : vector(width / 2, height / 2)
                            });
                    skSolve(sketch2);
                    opSweep(context, id + "sweep1", {
                                "profiles" : qCreatedBy(id + "sketch2", EntityType.FACE),
                                "path" : edges
                            });
                    setProperty(context, {
                                "entities" : qCreatedBy(id + "sweep1", EntityType.BODY),
                                "propertyType" : PropertyType.NAME,
                                "value" : "Chain Sweep, ~" ~ numPoints ~ " links"
                            });
                    if (definition.excludefromBOM)
                    {
                        setProperty(context, {
                                    "entities" : qCreatedBy(id + "sweep1", EntityType.BODY),
                                    "propertyType" : PropertyType.EXCLUDE_FROM_BOM,
                                    "value" : true
                                });
                    }
                }
                else
                {
                    var points = generatePoints(context, id + "generate1", edgePlane, edges, startPoint2d, numPoints + 2, pitch);

                    if (definition.errorType != ErrorType.IGNORE)
                    {
                        var contract = definition.errorType == ErrorType.CONTRACT;
                        numPoints += contract ? 1 : 0;
                        var offset = norm(points[0] - points[numPoints]) / numPoints * (contract ? -1 : 1);
                        points = generatePoints(context, id + "generate2", edgePlane, edges, startPoint2d, numPoints, pitch + offset);
                    }
                    // println(points);
                    var numLinks;
                    var linkMates;
                    var linkBodies;
                    if (definition.linkType == LinkType.CUSTOM)
                    {
                        numLinks = size(definition.links);
                        linkMates = makeArray(numLinks);
                        linkBodies = makeArray(numLinks);
                        for (var i = 0; i < numLinks; i += 1)
                        {
                            linkMates[i] = definition.links[i].mate;
                            linkBodies[i] = definition.links[i].body;
                        }
                    }
                    else if (definition.linkType == LinkType.STANDARD)
                    {
                        if (getLookupTable(ProfileTable, definition.profile) == "GB_Plastic") //check if gobilda plastic chain is selected
                        {
                            numLinks = 1;
                            linkMates = makeArray(1);
                            linkBodies = makeArray(2);
                            var instantiator = newInstantiator(id + "instantiator1");
                            linkBodies[0] = qBodyType(addInstance(instantiator, GOBILDA_PLASTIC::build, {}), BodyType.SOLID);
                            instantiate(context, instantiator);
                            linkMates[0] = qMateConnectorsOfParts(linkBodies[0]);
                        }
                        else
                        {
                            numLinks = 2;
                            linkMates = makeArray(3);
                            linkBodies = makeArray(3);
                            var instantiator = newInstantiator(id + "instantiator1");
                            linkBodies[0] = qBodyType(addInstance(instantiator, CHAIN_LINK::build, { "configuration" : {
                                                "Standard" : CHAIN_LINK::Standard_conf[getLookupTable(ProfileTable, definition.profile)],
                                                "whichlink" : CHAIN_LINK::whichlink_conf.inner
                                            } }), BodyType.SOLID);
                            linkBodies[1] = qBodyType(addInstance(instantiator, CHAIN_LINK::build, { "configuration" : {
                                                "Standard" : CHAIN_LINK::Standard_conf[getLookupTable(ProfileTable, definition.profile)],
                                                "whichlink" : CHAIN_LINK::whichlink_conf.outer
                                            } }), BodyType.SOLID);
                            linkBodies[2] = qBodyType(addInstance(instantiator, CHAIN_LINK::build, { "configuration" : {
                                                "Standard" : CHAIN_LINK::Standard_conf[getLookupTable(ProfileTable, definition.profile)],
                                                "whichlink" : CHAIN_LINK::whichlink_conf.connecting
                                            } }), BodyType.SOLID);
                            instantiate(context, instantiator);
                            linkMates[0] = qMateConnectorsOfParts(linkBodies[0]);
                            linkMates[1] = qMateConnectorsOfParts(linkBodies[1]);
                            linkMates[2] = qMateConnectorsOfParts(linkBodies[2]);
                        }
                    }
                    var transforms = makeArray(numLinks);
                    var instanceNames = makeArray(numLinks);
                    for (var i = 0; i < numLinks; i += 1)
                    {
                        var intPart = floor(numPoints / numLinks);
                        var fracPart = (numPoints / numLinks) - intPart;
                        var length = intPart + (i < fracPart * numLinks ? 1 : 0);
                        transforms[i] = makeArray(length);
                        instanceNames[i] = makeArray(length);
                    }

                    for (var i = 0; i < numPoints; i += 1)
                    {
                        var direction = normalize(points[(i + 1) % numPoints] - points[i]);
                        var angle = atan2(direction[1], direction[0]);
                        var j = i % numLinks;
                        var linkMate = evMateConnector(context, { "mateConnector" : linkMates[j] });
                        //var alignment = transform(line(linkMate.origin, linkMate.zAxis), line(WORLD_ORIGIN, edgePlane.normal));
                        var prealignment = fromWorld(coordSystem(linkMate.origin, linkMate.xAxis, linkMate.zAxis));
                        var alignment = toWorld(coordSystem(edgePlane));
                        var rotation = rotationAround(line(WORLD_ORIGIN, edgePlane.normal), angle);
                        var baseplane = plane(vector(0, 0, 0) * millimeter, edgePlane.normal, edgePlane.x);
                        var translation = transform(planeToWorld(baseplane, points[i]));
                        //var translation = transform(vector());
                        transforms[j][floor(i / numLinks)] = translation * rotation * alignment * prealignment;
                        instanceNames[j][floor(i / numLinks)] = "instance" ~ i ~ "_" ~ j;
                    }

                    var tools = qNothing();

                    if (((numPoints - 1) % 2 == 0) && size(linkBodies) != 2) //odd # of links
                    {
                        // resize array to account for last link generation
                        var j = (numPoints - 1) % numLinks;
                        var i = floor((numPoints - 1) / numLinks);
                        var transform = transforms[j][i];
                        var instanceName = instanceNames[(numPoints - 1) % numLinks][floor((numPoints - 1) / numLinks)];
                        transforms[j] = resize(transforms[j], i);
                        instanceNames[j] = resize(instanceNames[j], i);
                        opPattern(context, id + "patternLast", {
                                    "entities" : linkBodies[2],
                                    "transforms" : [transform],
                                    "instanceNames" : [instanceName]
                                });
                        tools = qUnion(tools, qCreatedBy(id + "patternLast", EntityType.BODY));
                    }
                    debug(context, edges, DebugColor.RED);


                    for (var i = 0; i < numLinks; i += 1)
                    {
                        opPattern(context, id + ("pattern" ~ i), {
                                    "entities" : linkBodies[i],
                                    "transforms" : transforms[i],
                                    "instanceNames" : instanceNames[i]
                                });
                        tools = qUnion(tools, qCreatedBy(id + ("pattern" ~ i), EntityType.BODY));
                    }
                    
                    opCreateCompositePart(context, id + "compositePart1", {
                                "bodies" : tools,
                                "closed" : true
                            });
                    if (definition.pathType == PathType.POINTS && definition.mates)
                    {
                        for (var i = 0; i < size(definition.sprockets); i += 1)
                        {
                            opMateConnector(context, id + ("mateConnector" ~ i), {
                                        "coordSystem" : matelocs[i],
                                        "owner" : qNthElement(tools, 0)
                                    });
                        }
                    }
                    setProperty(context, {
                                "entities" : qCreatedBy(id + "compositePart1", EntityType.BODY),
                                "propertyType" : PropertyType.NAME,
                                "value" : "Chain with " ~ toString(numPoints) ~ " links"
                            });
                    if (definition.excludefromBOM)
                    {
                        setProperty(context, {
                                    "entities" : qCreatedBy(id + "compositePart1", EntityType.BODY),
                                    "propertyType" : PropertyType.EXCLUDE_FROM_BOM,
                                    "value" : true
                                });
                    }
                    if (definition.linkType == LinkType.STANDARD)
                    {
                        opDeleteBodies(context, id + "deleteBodies1", { "entities" : qCreatedBy(id + "instantiator1") });
                    }
                }
            }
            setFeatureComputedParameter(context, id, { "name" : "teeth", "value" : toString(numPoints) ~ (isChain ? " links" : " teeth") });
        }
    });

function arcPoint(center, radius, angle)
{
    return center + radius * vector(cos(angle), sin(angle));
}

//given an estimate of the number of points that will be generated and the functional pitch, generate & return true point positions along path
function generatePoints(context, id, edgePlane, edges, startPoint2d, numPoints, pitch)
{
    var points = makeArray(numPoints); //numPoints is the estimate based on the length of the path
    points[0] = startPoint2d;
    var sketchesToDelete = qNothing();
    for (var i = 0; i < numPoints - 1; i += 1)
    {
        var sketchI = newSketchOnPlane(context, id + ("sketch" ~ i), { "sketchPlane" : edgePlane });
        if (i == 0)
            skCircle(sketchI, "circle1", { "center" : points[0], "radius" : pitch });
        else
        {
            var angle = atan2((points[i] - points[i - 1])[1], (points[i] - points[i - 1])[0]);
            skArc(sketchI, "arc1", {
                        "start" : arcPoint(points[i], pitch, angle - 90 * degree),
                        "mid" : arcPoint(points[i], pitch, angle),
                        "end" : arcPoint(points[i], pitch, angle + 90 * degree)
                    });
        }
        
        skSolve(sketchI);

        var intersection = evDistance(context, {
                "side0" : qCreatedBy(id + ("sketch" ~ i), EntityType.EDGE),
                "side1" : edges
            });
        
        sketchesToDelete = qUnion(sketchesToDelete, qCreatedBy(id + ("sketch" ~ i), EntityType.BODY));
        //skSolve(sketchI);
        var point = worldToPlane(edgePlane, intersection.sides[0].point);
        points[i + 1] = point;


        if (i >= numPoints - 10 && i < numPoints - 2)
        {
            if (evDistance(context, {
                                "side0" : intersection.sides[0].point,
                                "side1" : planeToWorld(edgePlane, startPoint2d)
                            }).distance < pitch)
            {
                debug(context, intersection.sides[0].point, DebugColor.RED);
                // points = resize(points, i+2);
                // return points;
            }
        }
    }
    opDeleteBodies(context, id + "delete", {
                    "entities" : sketchesToDelete
                });
    return points;
}
