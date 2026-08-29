FeatureScript 3044;
import(path : "onshape/std/common.fs", version : "3044.0");

/**
 * 带轮法兰样式：锥形墙（COTS 标准，默认）或平圆柱（固定 1mm 厚）。
 */
export enum FlangeStyle
{
    annotation { "Name" : "Conical (COTS)" }
    CONICAL,
    annotation { "Name" : "Flat cylinder (1mm)" }
    FLAT_CYLINDER
}

// 平圆柱法兰样式（固定）：轴向厚度 1mm，直径为法兰直径（> 齿顶）
const FLAT_FLANGE_LEN = 1 * millimeter;

/**
 * 六角轴孔规格：FRC 常用的 1/2 六角（对边距 12.7mm）与 3/8 六角（对边距
 * 9.525mm）。是否打孔、是否圆孔由布尔参数控制（precondition 只支持布尔条件）。
 */
export enum HexSize
{
    annotation { "Name" : "1/2 hex (FRC)" }
    HEX_1_2,
    annotation { "Name" : "3/8 hex (FRC)" }
    HEX_3_8
}

// FRC 六角轴孔对边距（across flats）：1/2 inch = 12.7mm，3/8 inch = 9.525mm
const HEX_HALF_AF = 0.5 * inch;
const HEX_3_8_AF = 0.375 * inch;

/**
 * Pulley Roller - 复合带轮滚轮
 *
 * 在一根空心管滚轮的端部圆环面上生成复合带轮零件（一个 part）：
 *   1. 按输入长度填充管子内孔（从所选端面向管内）
 *   2. 从端面沿轴向延伸主轴，默认直径与管子外径相同（与圆环面外环平齐）
 *   3. 沿轴排布多个同步带轮，每个带轮可独立设置齿形标准（GT2 2M/3M/5M/8M、
 *      HTD 3M/5M 共六种）/ 齿数 / 宽度，相邻带轮中心距可精确控制；节圆直径
 *      由齿数推导（pd = 齿数 x 皮带齿距 / PI），齿距恒为所选标准的皮带齿距
 *      （GT2 2M/3M/5M/8M 即 2/3/5/8mm，HTD 3M/5M 即 3/5mm），齿数变大带轮
 *      等比变大；齿顶外径严格按 SDP/SI 标准：OD = PD - 2U（U 值：
 *      2M/3M = 0.254/0.381，5M = 0.5715，8M = 0.6858，HTD 3M/5M =
 *      0.381/0.5715）；
 *      每个带轮两侧有挡边（法兰）防止带滑落，样式可选（Flange style）：
 *      锥形（默认，COTS 标准尺寸）：从齿顶锥形升起超过齿顶并保持；
 *      平圆柱：固定 1mm 厚、直径为法兰直径（> 齿顶）的圆柱。相邻两带轮
 *      之间的接口：法兰直径取两者较大值；锥形样式下两法兰厚度各保持
 *      生效值、法兰之间用同直径圆柱填充，平圆柱样式下两侧共用一个
 *      1mm 法兰（接口最小中心距相应少 1mm）
 *   4. 端面底领：贴端面先是固定 1mm 厚的圆柱（盖住圆环面外环边），再以
 *      最厚 2mm 的锥体收拢到带轮 1 法兰直径；之后以该直径的圆柱引导段一直
 *      延伸到带轮 1 的法兰（平齐衔接）。底领 + 引导段 + 管内填充与其余
 *      新几何全部合并为一个 part
 *   5. 全部新几何合并为单一零件（独立 part，不与原管子合并）
 *   6. 可选轴向孔（勾选 Add axial hole 后才显示相关参数）：可选从带轮侧
 *      外端面（默认）或管端侧填充端面开始打孔，可贯穿或指定深度（贯穿时
 *      不显示深度）；孔型可选 FRC 常用 1/2 六角（对边距 12.7mm）/ 3/8 六角
 *      （对边距 9.525mm）/ 自定义半径圆孔（勾选 Custom circular hole 后才
 *      显示半径）；孔中心可偏离零件轴线
 *
 * 齿形解析公式来自 trilobio 的 "Timing Belt Pulley"（GT2 2M/3M）；
 * GT2 5M/8M 与 HTD 3M/5M 采用各标准公布的名义齿形参数（近似）。
 */
annotation { "Feature Type Name" : "Pulley Roller" }
export const pulleyRoller = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Roller end face",
                     "Filter" : GeometryType.PLANE && EntityType.FACE,
                     "MaxNumberOfPicks" : 1,
                     "Description" : "Annular end face of the tube. Pulley axis is normal to this face and passes through its center" }
        definition.rollerFace is Query;

        annotation { "Name" : "Flip direction",
                     "Description" : "Reverse pulley extension direction (default: outward from the selected end face)" }
        definition.flip is boolean;

        annotation { "Name" : "Fill tube length",
                     "Description" : "Solid fill length inside the tube from the end face (0 = no fill)" }
        isLength(definition.fillLength, FILL_BOUNDS);

        annotation { "Name" : "Number of pulleys" }
        isInteger(definition.pulleyCount, PULLEY_COUNT_BOUNDS);

        annotation { "Name" : "Pulley 1 tooth profile",
                     "Description" : "Belt tooth standard for pulley 1 (GT2 2M/3M/5M/8M, HTD 3M/5M)" }
        definition.profile1 is ToothProfile;

        annotation { "Name" : "Pulley flange style",
                     "Description" : "Conical (default): COTS-style conical wall rising past the tooth tip. Flat cylinder: 1mm-thick cylinder at the flange diameter (larger than tooth tip) instead of the cone" }
        definition.flangeStyle is FlangeStyle;

        annotation { "Name" : "End collar overhang",
                     "Description" : "How far the collar extends beyond the tube OD / pulley-1 flange diameter at the end face (0 = flush). Collar: fixed 1mm cylinder + max-2mm taper; pulley flanges use COTS standard sizes" }
        isLength(definition.flangeOverhang, FLANGE_O_BOUNDS);

        annotation { "Name" : "Custom shaft diameter",
                     "Description" : "Check to set a custom shaft diameter. Default: shaft diameter = tube OD (flush with outer ring of the annular face)" }
        definition.customShaftDia is boolean;

        if (definition.customShaftDia)
        {
            annotation { "Name" : "Shaft diameter" }
            isLength(definition.shaftDiameter, SHAFT_DIA_BOUNDS);
        }

        annotation { "Name" : "Pulley 1 center offset",
                     "Description" : "Axial distance from end face to center of pulley 1" }
        isLength(definition.offset1, OFFSET_BOUNDS);

        annotation { "Name" : "Pulley 1 teeth",
                     "Description" : "Pitch diameter is derived: teeth x belt pitch / PI. More teeth = larger pulley, tooth spacing stays at the belt pitch" }
        isInteger(definition.teeth1, TEETH_BOUNDS);
        annotation { "Name" : "Pulley 1 width",
                     "Description" : "Pulley width (common standard: 6mm / 9mm)" }
        isLength(definition.width1, WIDTH_BOUNDS);

        annotation { "Name" : "Pulley 1-2 center distance",
                     "Description" : "Axial distance between pulley 1 and pulley 2 centers (used when Number of pulleys is 2 or more)" }
        isLength(definition.ctc1, CTC_BOUNDS);
        annotation { "Name" : "Pulley 2 tooth profile",
                     "Description" : "Used when Number of pulleys is 2 or more" }
        definition.profile2 is ToothProfile;

        annotation { "Name" : "Pulley 2 teeth",
                     "Description" : "Used when Number of pulleys is 2 or more. Pitch diameter is derived from teeth" }
        isInteger(definition.teeth2, TEETH_BOUNDS);
        annotation { "Name" : "Pulley 2 width",
                     "Description" : "Used when Number of pulleys is 2 or more" }
        isLength(definition.width2, WIDTH_BOUNDS);

        annotation { "Name" : "Pulley 2-3 center distance",
                     "Description" : "Axial distance between pulley 2 and pulley 3 centers (used when Number of pulleys is 3 or more)" }
        isLength(definition.ctc2, CTC_BOUNDS);
        annotation { "Name" : "Pulley 3 tooth profile",
                     "Description" : "Used when Number of pulleys is 3 or more" }
        definition.profile3 is ToothProfile;

        annotation { "Name" : "Pulley 3 teeth",
                     "Description" : "Used when Number of pulleys is 3 or more. Pitch diameter is derived from teeth" }
        isInteger(definition.teeth3, TEETH_BOUNDS);
        annotation { "Name" : "Pulley 3 width",
                     "Description" : "Used when Number of pulleys is 3 or more" }
        isLength(definition.width3, WIDTH_BOUNDS);

        annotation { "Name" : "Pulley 3-4 center distance",
                     "Description" : "Axial distance between pulley 3 and pulley 4 centers (used when Number of pulleys is 4)" }
        isLength(definition.ctc3, CTC_BOUNDS);
        annotation { "Name" : "Pulley 4 tooth profile",
                     "Description" : "Used when Number of pulleys is 4" }
        definition.profile4 is ToothProfile;

        annotation { "Name" : "Pulley 4 teeth",
                     "Description" : "Used when Number of pulleys is 4. Pitch diameter is derived from teeth" }
        isInteger(definition.teeth4, TEETH_BOUNDS);
        annotation { "Name" : "Pulley 4 width",
                     "Description" : "Used when Number of pulleys is 4" }
        isLength(definition.width4, WIDTH_BOUNDS);

        annotation { "Name" : "Add axial hole",
                     "Description" : "Cut a hole along the part axis. All hole parameters below appear only when this is checked" }
        definition.addHole is boolean;

        if (definition.addHole)
        {
            annotation { "Name" : "Hole from pulley end",
                         "Description" : "Start the hole at the outer pulley end face (default). Uncheck to start from the tube-side end face (the filled plug end inside the tube)" }
            definition.holeFromPulleyEnd is boolean;
            annotation { "Name" : "Custom circular hole",
                         "Description" : "Use a custom-radius circular hole instead of an FRC hex bore" }
            definition.holeCircle is boolean;

            if (definition.holeCircle)
            {
                annotation { "Name" : "Hole radius",
                             "Description" : "Radius of the circular hole" }
                isLength(definition.holeRadius, HOLE_R_BOUNDS);
            }
            else
            {
                annotation { "Name" : "Hex size",
                             "Description" : "FRC hex shaft bore size (across flats: 1/2 = 12.7mm, 3/8 = 9.525mm)" }
                definition.hexSize is HexSize;
            }

            annotation { "Name" : "Hole through all",
                         "Description" : "Cut the hole through the entire part (ignores Hole depth)" }
            definition.holeThrough is boolean;

            if (!definition.holeThrough)
            {
                annotation { "Name" : "Hole depth",
                             "Description" : "Hole depth from the outer end face" }
                isLength(definition.holeDepth, HOLE_DEPTH_BOUNDS);
            }

            annotation { "Name" : "Hole offset",
                         "Description" : "Distance the hole center is offset from the part axis (0 = centered)" }
            isLength(definition.holeOffset, HOLE_OFF_BOUNDS);
        }
    }
    {
        doPulleyRoller(context, id, definition);
    },
    {
        "rollerFace" : qNothing(),
        "flip" : false,
        "fillLength" : 20 * millimeter,
        "pulleyCount" : 2,
        "profile1" : ToothProfile.GT2_3M,
        "profile2" : ToothProfile.GT2_3M,
        "profile3" : ToothProfile.GT2_3M,
        "profile4" : ToothProfile.GT2_3M,
        "flangeStyle" : FlangeStyle.CONICAL,
        "flangeOverhang" : 1 * millimeter,
        "customShaftDia" : false,
        "shaftDiameter" : 10 * millimeter,
        "offset1" : 12 * millimeter,
        "teeth1" : 28,
        "width1" : 6 * millimeter,
        "ctc1" : 20 * millimeter,
        "teeth2" : 32,
        "width2" : 6 * millimeter,
        "ctc2" : 20 * millimeter,
        "teeth3" : 24,
        "width3" : 6 * millimeter,
        "ctc3" : 20 * millimeter,
        "teeth4" : 24,
        "width4" : 6 * millimeter,
        "addHole" : true,
        "holeFromPulleyEnd" : true,
        "holeCircle" : false,
        "hexSize" : HexSize.HEX_1_2,
        "holeThrough" : true,
        "holeDepth" : 20 * millimeter,
        "holeRadius" : 6.35 * millimeter,
        "holeOffset" : 0 * millimeter
    });

function doPulleyRoller(context is Context, id is Id, definition is map)
{
    if (isQueryEmpty(context, definition.rollerFace))
    {
        throw regenError("请选择管子端部的圆环面（Roller end face）。", ["rollerFace"]);
    }

    const face = evaluateQuery(context, definition.rollerFace)[0];

    // 轴向：所选平面面法线；圆心：由圆环边几何直接得出
    const facePlane = evPlane(context, { "face" : face });
    var axis = facePlane.normal;

    // 圆环面相邻边中过滤出真实的圆边（排除零长度接缝边），用圆定义得到半径与圆心
    const edges = evaluateQuery(context, qAdjacent(face, AdjacencyType.EDGE));
    var radii = [];
    var circleCenter = facePlane.origin;
    for (var i = 0; i < size(edges); i += 1)
    {
        if (evLength(context, { "entities" : edges[i] }) > 1e-6 * meter)
        {
            const def = evCurveDefinition(context, { "edge" : edges[i] });
            if (def.radius != undefined)
            {
                radii = append(radii, def.radius);
                circleCenter = def.coordSystem.origin;
            }
        }
    }
    if (size(radii) < 2)
    {
        throw regenError("所选面不是圆环面。请选择管子端部的环形端面（中间镂空）。", ["rollerFace"]);
    }
    const outerR = max(radii);
    const innerR = min(radii);
    const center = circleCenter;

    // 自动判断伸出方向：面在管子哪一端，带轮就往另一侧伸出（flip 可强制反转）
    const tubeBb = evBox3d(context, { "topology" : qOwnerBody(face) });
    const tubeMid = (tubeBb.minCorner + tubeBb.maxCorner) / 2;
    if (dot(center - tubeMid, axis) < 0)
    {
        axis = -axis;
    }
    if (definition.flip)
    {
        axis = -axis;
    }

    // 主轴半径
    var shaftR = outerR;
    if (definition.customShaftDia)
    {
        shaftR = definition.shaftDiameter / 2;
    }

    // 收集各带轮参数（每个带轮可独立选择齿形标准）
    var profiles = [definition.profile1];
    var teeth = [definition.teeth1];
    var widths = [definition.width1];
    var centers = [definition.offset1];
    if (definition.pulleyCount >= 2)
    {
        profiles = append(profiles, definition.profile2);
        teeth = append(teeth, definition.teeth2);
        widths = append(widths, definition.width2);
        centers = append(centers, definition.ctc1);
    }
    if (definition.pulleyCount >= 3)
    {
        profiles = append(profiles, definition.profile3);
        teeth = append(teeth, definition.teeth3);
        widths = append(widths, definition.width3);
        centers = append(centers, definition.ctc2);
    }
    if (definition.pulleyCount >= 4)
    {
        profiles = append(profiles, definition.profile4);
        teeth = append(teeth, definition.teeth4);
        widths = append(widths, definition.width4);
        centers = append(centers, definition.ctc3);
    }

    const n = size(teeth);

    // 底领参数（仅作用于端面底领，径向超出量）
    const collarOverhang = definition.flangeOverhang;

    // 各带轮的齿形参数与 COTS 法兰参数（FT 轴向厚度 / FH 高出齿顶高度）、
    // 节圆直径（由齿数推导：pd = teeth x P / PI，齿距恒为皮带齿距 P，
    // 齿数变大带轮等比变大）、齿顶半径与自身法兰半径
    var profs = [];
    var pds = [];
    var fts = [];
    var tipRs = [];
    var flangeRs = [];
    for (var i = 0; i < n; i += 1)
    {
        const p = ToothProfileDefinitions[profiles[i]];
        profs = append(profs, p);
        pds = append(pds, teeth[i] * p["P"] / PI);
        fts = append(fts, p["FT"]);
        tipRs = append(tipRs, pulleyTipRadius(teeth[i], p, pds[i]));
        flangeRs = append(flangeRs, tipRs[i] + p["FH"]);
    }

    // 法兰样式生效厚度：锥形样式用各齿形 COTS 值；平圆柱样式统一为固定
    // 1mm 厚（直径不变，仍为法兰直径 > 齿顶）
    const flatFlange = definition.flangeStyle == FlangeStyle.FLAT_CYLINDER;
    var effFts = [];
    for (var i = 0; i < n; i += 1)
    {
        effFts = append(effFts, flatFlange ? FLAT_FLANGE_LEN : fts[i]);
    }

    // 计算各带轮中心 z 位置（centers[0] 为面到带轮 1 中心，其后为相邻中心距增量）
    var z = [centers[0]];
    for (var i = 1; i < n; i += 1)
    {
        z = append(z, z[i - 1] + centers[i]);
    }

    // 校验：带轮（含法兰）不得伸进管子、不得相互重叠、齿根必须粗于主轴
    if (z[0] < widths[0] / 2 + effFts[0])
    {
        throw regenError("Pulley 1 center offset 太小（不小于带宽一半 + 法兰厚度，即 "
                ~ toString(widths[0] / 2 + effFts[0]) ~ "），否则左侧法兰会伸进管子。", ["offset1"]);
    }
    // 底领（圆柱 + 锥体收拢段）必须在带轮 1 端面之前完成，避免盖住齿形
    if (z[0] - widths[0] / 2 < COLLAR_CYL_LEN + COLLAR_CONE_LEN)
    {
        throw regenError("Pulley 1 center offset 太小：端面底领（圆柱 + 锥体）需在带轮 1 之前完成收拢，最小 offset ≈ "
                ~ toString(widths[0] / 2 + COLLAR_CYL_LEN + COLLAR_CONE_LEN) ~ "。", ["offset1"]);
    }
    for (var i = 1; i < n; i += 1)
    {
        // 锥形样式：法兰允许外端面恰好重合（交于一个圆环，体积交叠为零），
        // 但不允许真正交叠：接口法兰直径取两者较大值、厚度各保持生效值，
        // 最小 CTC = 半宽之和 + 两侧法兰各自厚度（此时两法兰外端面共面）。
        // 平圆柱样式：相邻两侧 1mm 法兰共用一个，最小 CTC = 半宽之和 + 1mm
        const minCtc = (widths[i] + widths[i - 1]) / 2
                + (flatFlange ? FLAT_FLANGE_LEN : effFts[i - 1] + effFts[i]);
        if (z[i] - z[i - 1] < minCtc - 1e-6 * meter)
        {
            throw regenError("带轮 " ~ toString(i) ~ " 与带轮 " ~ toString(i + 1)
                    ~ " 中心距太小，法兰交叠。最小值为 " ~ toString(minCtc)
                    ~ (flatFlange ? "（两侧共用一个 1mm 法兰）。" : "（两法兰外端面恰好重合）。"), ["ctc" ~ toString(i)]);
        }
    }
    for (var i = 0; i < n; i += 1)
    {
        const rootR = pulleyRootRadius(teeth[i], profs[i], pds[i]);
        if (shaftR >= rootR)
        {
            throw regenError("主轴直径对于带轮 " ~ toString(i + 1) ~ " 太大（齿根半径约 "
                    ~ toString(2 * rootR) ~ "）。请缩小轴径或增大带轮。");
        }
    }

    // 主轴长度 = 最后一个带轮末端 + 右侧法兰
    const totalLen = z[n - 1] + widths[n - 1] / 2 + effFts[n - 1];

    var newBodies = [];

    // 1. 填充管子内孔
    //    填充体从端面外 FILL_OVERLAP 处沿 -axis 拉伸 fillLength + FILL_OVERLAP：
    //    伸出端面的 0.5mm 完全位于底领实心盘（r <= collarFace）内部，外观不可见，
    //    但使填充与底领产生真实体积重叠（而非仅 z=0 面贴面接触），
    //    布尔 union 才能可靠地把端面内填充与端面外几何缝合成单一 part
    if (definition.fillLength > 0 * millimeter)
    {
        const fillSketch = newSketchOnPlane(context, id + "fillSketch", {
                    "sketchPlane" : plane(center + axis * FILL_OVERLAP, axis)
                });
        skCircle(fillSketch, "fill", {
                    "center" : vector(0, 0) * millimeter,
                    "radius" : innerR
                });
        skSolve(fillSketch);
        opExtrude(context, id + "fillExtrude", {
                    "entities" : qSketchRegion(id + "fillSketch"),
                    "direction" : -axis,
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : definition.fillLength + FILL_OVERLAP
                });
        newBodies = append(newBodies, qCreatedBy(id + "fillExtrude", EntityType.BODY));
    }

    // 2. 主轴：从端面延伸到最后一个带轮末端，默认与管外径平齐
    const shaftSketch = newSketchOnPlane(context, id + "shaftSketch", {
                "sketchPlane" : plane(center, axis)
            });
    skCircle(shaftSketch, "shaft", {
                "center" : vector(0, 0) * millimeter,
                "radius" : shaftR
            });
    skSolve(shaftSketch);
    opExtrude(context, id + "shaftExtrude", {
                "entities" : qSketchRegion(id + "shaftSketch"),
                "direction" : axis,
                "endBound" : BoundingType.BLIND,
                "endDepth" : totalLen
            });
    newBodies = append(newBodies, qCreatedBy(id + "shaftExtrude", EntityType.BODY));

    // 2b. 引导段：与带轮 1 法兰同直径（tipR + FH）的圆柱，从端面一直延伸到
    //     带轮 1 左法兰，与法兰平齐衔接
    const leadR = flangeRs[0]; // 引导段半径 = 带轮 1 左法兰半径
    const leadLen = z[0] - widths[0] / 2; // 引导段长度：端面 -> 带轮 1 左端面
    const leadSketch = newSketchOnPlane(context, id + "leadSketch", {
                "sketchPlane" : plane(center, axis)
            });
    skCircle(leadSketch, "lead", {
                "center" : vector(0, 0) * millimeter,
                "radius" : leadR
            });
    skSolve(leadSketch);
    opExtrude(context, id + "leadExtrude", {
                "entities" : qSketchRegion(id + "leadSketch"),
                "direction" : axis,
                "endBound" : BoundingType.BLIND,
                "endDepth" : leadLen
            });
    newBodies = append(newBodies, qCreatedBy(id + "leadExtrude", EntityType.BODY));

    // 3. 各带轮：齿形草图（放在带轮起始端面）+ 单向拉伸一个宽度
    for (var i = 0; i < n; i += 1)
    {
        const pulleyPlane = plane(center + axis * (z[i] - widths[i] / 2), axis);
        drawPulleyTeeth(context, id + ("pulley" ~ toString(i)), pulleyPlane, teeth[i], profs[i], pds[i]);

        opExtrude(context, id + ("pulleyExtrude" ~ toString(i)), {
                    "entities" : qSketchRegion(id + ("pulley" ~ toString(i))),
                    "direction" : axis,
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : widths[i]
                });
        newBodies = append(newBodies, qCreatedBy(id + ("pulleyExtrude" ~ toString(i)), EntityType.BODY));
    }

    // 4. 法兰（旋转成型，样式可选）：
    //    - 锥形（默认）：COTS 尺寸，贴带轮端面处与齿顶平齐，锥形升起超过
    //      齿顶并保持
    //    - 平圆柱：固定 1mm 厚、直径为法兰直径（> 齿顶）的圆柱
    //    - pulley 1 左侧 / 最后一个 pulley 右侧：各自 COTS 标准尺寸
    //    - 相邻两个 pulley 之间的接口：若一侧法兰更大，另一侧法兰也采用
    //      该更大尺寸（半径取两者较大值），两个法兰之间用与法兰同直径的
    //      圆柱填充，形成连续过渡
    for (var i = 0; i < n; i += 1)
    {
        if (i == 0)
        {
            // pulley 1 左侧法兰（自身 COTS 尺寸）
            const flangeIdL = id + "flangeL0";
            makeFlange(context, flangeIdL, center, axis, z[0] - widths[0] / 2, -axis, effFts[0], tipRs[0], flangeRs[0], flatFlange);
            newBodies = append(newBodies, qCreatedBy(flangeIdL + "revolve", EntityType.BODY));
        }

        if (i < n - 1)
        {
            // 接口 i-(i+1)：法兰直径取两者较大值
            const frIface = max(flangeRs[i], flangeRs[i + 1]);

            if (flatFlange)
            {
                // 平圆柱样式：相邻两侧 1mm 法兰共用一个 —— 不再各自生成，
                // 接口缝隙（含共用法兰，缝隙 >= 1mm 由校验保证）整体用与
                // 法兰同直径的圆柱填充（直径一致，等效于共用法兰 + 填充）
                const cylStart = z[i] + widths[i] / 2;
                const cylLen = z[i + 1] - widths[i + 1] / 2 - cylStart;
                const gapSketch = newSketchOnPlane(context, id + ("gapSketch" ~ toString(i)), {
                            "sketchPlane" : plane(center + axis * cylStart, axis)
                        });
                skCircle(gapSketch, "gap", {
                            "center" : vector(0, 0) * millimeter,
                            "radius" : frIface
                        });
                skSolve(gapSketch);
                opExtrude(context, id + ("gapExtrude" ~ toString(i)), {
                            "entities" : qSketchRegion(id + ("gapSketch" ~ toString(i))),
                            "direction" : axis,
                            "endBound" : BoundingType.BLIND,
                            "endDepth" : cylLen
                        });
                newBodies = append(newBodies, qCreatedBy(id + ("gapExtrude" ~ toString(i)), EntityType.BODY));
            }
            else
            {
                // 锥形样式：厚度各保持生效值
                // 带轮 i 右侧法兰：从右端面沿 +axis 伸出 effFts[i] 厚
                const flangeIdR = id + ("flangeR" ~ toString(i));
                makeFlange(context, flangeIdR, center, axis, z[i] + widths[i] / 2, axis, effFts[i], tipRs[i], frIface, flatFlange);
                newBodies = append(newBodies, qCreatedBy(flangeIdR + "revolve", EntityType.BODY));

                // 带轮 i+1 左侧法兰：从左端面沿 -axis 伸出 effFts[i+1] 厚
                const flangeIdL = id + ("flangeL" ~ toString(i + 1));
                makeFlange(context, flangeIdL, center, axis, z[i + 1] - widths[i + 1] / 2, -axis, effFts[i + 1], tipRs[i + 1], frIface, flatFlange);
                newBodies = append(newBodies, qCreatedBy(flangeIdL + "revolve", EntityType.BODY));

                // 填充圆柱：与接口法兰同直径，位于两个法兰外端面之间
                const cylStart = z[i] + widths[i] / 2 + effFts[i];
                const cylLen = z[i + 1] - widths[i + 1] / 2 - effFts[i + 1] - cylStart;
                if (cylLen > 0 * millimeter)
                {
                    const gapSketch = newSketchOnPlane(context, id + ("gapSketch" ~ toString(i)), {
                                "sketchPlane" : plane(center + axis * cylStart, axis)
                            });
                    skCircle(gapSketch, "gap", {
                                "center" : vector(0, 0) * millimeter,
                                "radius" : frIface
                            });
                    skSolve(gapSketch);
                    opExtrude(context, id + ("gapExtrude" ~ toString(i)), {
                                "entities" : qSketchRegion(id + ("gapSketch" ~ toString(i))),
                                "direction" : axis,
                                "endBound" : BoundingType.BLIND,
                                "endDepth" : cylLen
                            });
                    newBodies = append(newBodies, qCreatedBy(id + ("gapExtrude" ~ toString(i)), EntityType.BODY));
                }
            }
        }
        else
        {
            // 最后一个 pulley 右侧法兰（自身 COTS 尺寸）
            const flangeIdR = id + ("flangeR" ~ toString(i));
            makeFlange(context, flangeIdR, center, axis, z[i] + widths[i] / 2, axis, effFts[i], tipRs[i], flangeRs[i], flatFlange);
            newBodies = append(newBodies, qCreatedBy(flangeIdR + "revolve", EntityType.BODY));
        }
    }

    // 5. 底领：贴端面先是轴向厚度 1mm（COLLAR_CYL_LEN，固定）的圆柱（半径
    //    collarFace，盖住圆环面外环边），再以最厚 2mm（COLLAR_CONE_LEN，固定）
    //    的锥体收拢到带轮 1 法兰半径 leadR；之后引导段保持 leadR 直到带轮 1 法兰
    const collarFace = max(outerR, leadR) + collarOverhang; // 圆柱段半径
    const collarId = id + "collar";
    makeCollar(context, collarId, center, axis, COLLAR_CYL_LEN, COLLAR_CONE_LEN, collarFace, leadR);
    newBodies = append(newBodies, qCreatedBy(collarId + "revolve", EntityType.BODY));

    // 6. 合并：新几何（填充 + 主轴 + 引导段 + 各带轮 + 挡边 + 底领）合并为单一零件，不与原管子合并。
    //    采用参考实现（alexkempen robot pulley / imants chain / abenstirling）的标准
    //    union 写法：opBoolean 只传 tools（qUnion 全部新实体）不传 targets，
    //    tools 内所有实体互相合并
    if (size(evaluateQuery(context, qUnion(newBodies))) > 1)
    {
        opBoolean(context, id + "union", {
                    "tools" : qUnion(newBodies),
                    "operationType" : BooleanOperationType.UNION
                });

        // 自检：合并后必须只剩 1 个实体（qUnion(newBodies) 惰性重解析，
        // 工具已被消耗，只解析出存活的目标体）。多于 1 个即有几何块未缝合，
        // 报出各实体的包围盒以便定位
        const merged = evaluateQuery(context, qUnion(newBodies));
        if (size(merged) > 1)
        {
            var detail = "";
            for (var m = 0; m < size(merged); m += 1)
            {
                const bb = evBox3d(context, { "topology" : merged[m] });
                detail = detail ~ " | 实体" ~ toString(m + 1) ~ " min="
                        ~ toString(bb.minCorner) ~ " max=" ~ toString(bb.maxCorner);
            }
            throw regenError("[combine-two-parts] 合并后仍有 " ~ toString(size(merged))
                    ~ " 个实体（端面内填充与端面外几何未缝合）：" ~ detail);
        }
    }

    // 7. 轴向孔：从所选端面沿轴向向内切孔（可贯穿或指定深度），
    //    孔中心可偏离轴线（沿垂直于轴的固定方向偏移 holeOffset）
    if (definition.addHole)
    {
        // 孔起始端：默认从带轮侧最外端（z = totalLen）沿 -axis 切入；
        // 关闭 "Hole from pulley end" 时从管端侧（z = -fillLength）沿 +axis
        // 切入。贯穿时深度覆盖全长（两个方向的轴向范围相同）
        const fromPulleyEnd = definition.holeFromPulleyEnd;
        const zStart = fromPulleyEnd ? totalLen : -definition.fillLength;
        const cutDir = fromPulleyEnd ? -axis : axis;
        const depth = definition.holeThrough ? (totalLen + definition.fillLength) : definition.holeDepth;

        const holeSk = newSketchOnPlane(context, id + "holeSketch", {
                    "sketchPlane" : plane(center + axis * zStart, cutDir)
                });
        if (definition.holeCircle)
        {
            skCircle(holeSk, "hole", {
                        "center" : vector(definition.holeOffset, 0 * millimeter),
                        "radius" : definition.holeRadius
                    });
        }
        else
        {
            // 六角孔：对边距 AF，外接圆半径 R = AF / sqrt(3)，顶点角 0/60/.../300。
            // 本标准库的 cos/sin 接受带角度单位的参数（同 trilobio 齿形代码的
            // cos(radian * ...) 写法），故乘 radian；顶点坐标单位最后恢复
            const af = definition.hexSize == HexSize.HEX_1_2 ? HEX_HALF_AF : HEX_3_8_AF;
            const off = definition.holeOffset / millimeter; // 无量纲（mm 数值）
            const hexR = af / millimeter / sqrt(3); // 无量纲外接圆半径（mm 数值）
            var hexPts = [];
            for (var v = 0; v < 6; v += 1)
            {
                const ang = radian * v * PI / 3;
                hexPts = append(hexPts, vector((off + hexR * cos(ang)) * millimeter, (hexR * sin(ang)) * millimeter));
            }
            for (var e = 0; e < 6; e += 1)
            {
                skLineSegment(holeSk, "hex" ~ toString(e), {
                            "start" : hexPts[e],
                            "end" : hexPts[(e + 1) % 6]
                        });
            }
        }
        skSolve(holeSk);

        opExtrude(context, id + "holeExtrude", {
                    "entities" : qSketchRegion(id + "holeSketch"),
                    "direction" : cutDir,
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : depth
                });

        opBoolean(context, id + "holeCut", {
                    // 目标用 qUnion(newBodies)（惰性）：布尔 union 不创建新实体
                    // （工具被消耗、目标体原地修改，qCreatedBy(id+"union") 解析为空），
                    // 合并后此查询只剩存活的目标体；未合并场景则包含全部实体
                    "targets" : qUnion(newBodies),
                    "tools" : qCreatedBy(id + "holeExtrude", EntityType.BODY),
                    "operationType" : BooleanOperationType.SUBTRACTION
                });
    }
}

/**
 * 齿形缩放系数：齿顶（外圆）严格按 SDP/SI 标准 OD = PD - 2U（U 为皮带
 * 齿顶线到节线的距离，见 ToothProfileDefinitions 中各齿形的 "U"）。
 * 齿形点整体等比缩放使外圆落在标准 OD 上；齿槽的角度位置（i*2π/t）不变，
 * 皮带在节圆（PD/2 = t*P/2π）上的啮合节距仍恒为 P。
 * 解析齿形点坐标为无量纲数值，故缩放系数为无量纲数。
 */
function toothScale(t is number, profile is map, pd is ValueWithUnits) returns number
{
    const pts = computeGtToothPoints(t, profile);
    const tipR = (pd / 2 - profile["U"]) / millimeter;
    // pts.D 为 Vector：先取模长（norm 对 Vector 直接可用，见
    // getArcMidPointShorter），再除以 millimeter 转为无量纲数值
    const dNorm = norm(pts.D) / millimeter;
    return tipR / dNorm;
}

/**
 * 在给定平面上画出完整带轮齿形草图（t 个齿沿圆周闭合）。
 * 齿形点由 GT 标准参数解析求出，再按 toothScale 等比缩放使外圆 = PD - 2U。
 * 返回齿根半径（缩放后）。
 */
function drawPulleyTeeth(context is Context, id is Id, sketchPlane is Plane, t is number, profile is map, pd is ValueWithUnits)
{
    const P = profile["P"];
    const scale = toothScale(t, profile, pd);
    const pts = computeGtToothPoints(t, profile);
    const alpha = pts.alpha;

    const sk = newSketchOnPlane(context, id, { "sketchPlane" : sketchPlane });

    for (var i = 0; i < t; i += 1)
    {
        const iString = toString(i);
        const rMat = [[cos(i * alpha), -sin(i * alpha)], [sin(i * alpha), cos(i * alpha)]] as Matrix;

        skArc(sk, "arcAB" ~ iString, {
                    "start" : rMat * (pts.A * scale),
                    "mid" : rMat * (pts.ABM * scale),
                    "end" : rMat * (pts.B * scale)
                });
        skArc(sk, "arcBC" ~ iString, {
                    "start" : rMat * (pts.B * scale),
                    "mid" : rMat * (pts.BCM * scale),
                    "end" : rMat * (pts.C * scale)
                });
        skArc(sk, "arcCD" ~ iString, {
                    "start" : rMat * (pts.C * scale),
                    "mid" : rMat * (pts.CDM * scale),
                    "end" : rMat * (pts.D * scale)
                });
        skArc(sk, "arcDE" ~ iString, {
                    "start" : rMat * (pts.D * scale),
                    "mid" : rMat * (pts.DEM * scale),
                    "end" : rMat * (pts.E * scale)
                });
        skArc(sk, "arcEF" ~ iString, {
                    "start" : rMat * (pts.E * scale),
                    "mid" : rMat * (pts.EFM * scale),
                    "end" : rMat * (pts.F * scale)
                });
        skArc(sk, "arcFG" ~ iString, {
                    "start" : rMat * (pts.F * scale),
                    "mid" : rMat * (pts.FGM * scale),
                    "end" : rMat * (pts.G * scale)
                });
    }
    skSolve(sk);

    return norm(pts.ABM) * scale;
}

/**
 * 带轮齿根半径（齿形点缩放后，缩放系数同 toothScale）。
 */
function pulleyRootRadius(t is number, profile is map, pd is ValueWithUnits) returns ValueWithUnits
{
    const pts = computeGtToothPoints(t, profile);
    const scale = toothScale(t, profile, pd);
    return norm(pts.ABM) * scale;
}

/**
 * 带轮齿顶（外圆）半径：严格按 SDP/SI 标准 OD = PD - 2U。
 */
function pulleyTipRadius(t is number, profile is map, pd is ValueWithUnits) returns ValueWithUnits
{
    return pd / 2 - profile["U"];
}

/**
 * 带轮法兰（旋转成型）：
 *   起始于 zFace（轴向位置，贴带轮端面），沿 xDir 方向伸出 ft 厚。
 *   - 锥形样式（默认）：贴带轮端面处与齿顶平齐（rFace），先以约 45 度锥面
 *     快速升起超过齿顶（升高 rOuter - rFace），之后保持 rOuter 直到法兰
 *     外端 —— 立在齿顶上方的锥形墙，挡住带防止滑落
 *   - 平圆柱样式（flat = true）：直径为法兰直径 rOuter（> 齿顶）的圆柱，
 *     厚度 ft（固定 1mm），替代锥形墙
 * 截面草图放在过 center + axis*zFace、x 轴为 xDir 的平面上，绕轴线整周旋转。
 */
function makeFlange(context is Context, id is Id, center is Vector, axis is Vector, zFace is ValueWithUnits, xDir is Vector, ft is ValueWithUnits, rFace is ValueWithUnits, rOuter is ValueWithUnits, flat is boolean)
{
    const base = center + axis * zFace;
    const sk = newSketchOnPlane(context, id + "sketch", {
                "sketchPlane" : plane(base, perpendicularVector(axis), xDir)
            });

    // 截面轮廓（x = 沿 xDir 的轴向距离，y = 半径），y=0 边位于旋转轴上
    var points;
    if (flat)
    {
        // 平圆柱：整段保持 rOuter（> 齿顶）
        points = [
                    vector(0 * millimeter, 0 * millimeter),
                    vector(0 * millimeter, rOuter),
                    vector(ft, rOuter),
                    vector(ft, 0 * millimeter)
                ];
    }
    else if (rOuter > rFace && rOuter - rFace < ft)
    {
        // 法兰：45 度锥面升起段（轴向 = 径向升高量）+ 保持段（锥形墙立住超过齿顶）
        const rise = rOuter - rFace;
        points = [
                    vector(0 * millimeter, 0 * millimeter),
                    vector(0 * millimeter, rFace),
                    vector(rise, rOuter),
                    vector(ft, rOuter),
                    vector(ft, 0 * millimeter)
                ];
    }
    else
    {
        // 纯锥形：升高量超过厚度时法兰为全锥（正常 COTS 值不会走到该分支）
        points = [
                    vector(0 * millimeter, 0 * millimeter),
                    vector(0 * millimeter, rFace),
                    vector(ft, rOuter),
                    vector(ft, 0 * millimeter)
                ];
    }

    for (var j = 0; j < size(points); j += 1)
    {
        skLineSegment(sk, "l" ~ toString(j), {
                    "start" : points[j],
                    "end" : points[(j + 1) % size(points)]
                });
    }
    skSolve(sk);

    opRevolve(context, id + "revolve", {
                "entities" : qCreatedBy(id + "sketch", EntityType.FACE),
                "axis" : line(center, axis),
                "angleForward" : 360 * degree
            });
}

/**
 * 端面底领（旋转成型）：贴所选端面先是轴向厚度 cylLen 的圆柱（半径 rFace，
 * 盖住圆环面外环边），随后锥体在轴向 coneLen（最厚 2mm）内收拢到 rOuter
 * （带轮 1 法兰半径），与引导段圆柱平齐衔接；之后引导段保持 rOuter 直到
 * 带轮 1 法兰。截面草图平面过 center 且包含轴线（x 轴沿 axis 方向），
 * 绕轴线整周旋转。
 */
function makeCollar(context is Context, id is Id, center is Vector, axis is Vector, cylLen is ValueWithUnits, coneLen is ValueWithUnits, rFace is ValueWithUnits, rOuter is ValueWithUnits)
{
    const sk = newSketchOnPlane(context, id + "sketch", {
                "sketchPlane" : plane(center, perpendicularVector(axis), axis)
            });

    // 截面轮廓（x = 沿 axis 的轴向距离，y = 半径），y=0 边位于旋转轴上
    const points = [
                vector(0 * millimeter, 0 * millimeter),
                vector(0 * millimeter, rFace),
                vector(cylLen, rFace),
                vector(cylLen + coneLen, rOuter),
                vector(cylLen + coneLen, 0 * millimeter)
            ];

    for (var j = 0; j < size(points); j += 1)
    {
        skLineSegment(sk, "l" ~ toString(j), {
                    "start" : points[j],
                    "end" : points[(j + 1) % size(points)]
                });
    }
    skSolve(sk);

    opRevolve(context, id + "revolve", {
                "entities" : qCreatedBy(id + "sketch", EntityType.FACE),
                "axis" : line(center, axis),
                "angleForward" : 360 * degree
            });
}

/**
 * GT2 齿形单齿特征点解析解（来自 trilobio "Timing Belt Pulley"）。
 * 坐标以带轮轴心为原点；A..D 为齿槽一侧，DE 为以原点为圆心的齿顶外圆弧，
 * E..G 为旋转 -2π/t 后的另一侧，t 个齿首尾相接铺满整圈。
 */
function computeGtToothPoints(t is number, p is map)
{
    const P = p["P"];
    const RAB = p["R3"];
    const RBC = p["R2"];
    const RCD = p["R1"];
    const b = p["b"];
    const h = p["h"];
    const PLD = p["PLD"];

    const alpha = 2 * P / (P * t / PI) * radian;

    const AX = (-2 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 + RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) + RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) - 2 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 + RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) + RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) + 256 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 18 - 1152 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 16 + 2112 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 + 256 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 * cos(radian * PI * b / (P * t)) ^ 6 - 2016 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 - 384 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 * cos(radian * PI * b / (P * t)) ^ 6 + 1056 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 + 192 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 * cos(radian * PI * b / (P * t)) ^ 6 - 288 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 - 32 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 6 * cos(radian * PI * b / (P * t)) ^ 6 + 32 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 6) * sin(radian * 2 * PI * b / (P * t)) / (RAB ^ 2 - RBC ^ 2);
    const AY = (4 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 4 - 4 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 + RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) - 2 * RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) * sin(radian * PI * b / (P * t)) ^ 2 + RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) + 4 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 4 - 4 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 - 2 * RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) * sin(radian * PI * b / (P * t)) ^ 2 + RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) - RBC ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) - 512 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 20 + 2560 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 18 - 5248 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 16 - 512 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 * cos(radian * PI * b / (P * t)) ^ 6 + 5632 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 + 1024 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 * cos(radian * PI * b / (P * t)) ^ 6 - 3328 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 - 640 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 * cos(radian * PI * b / (P * t)) ^ 6 + 1024 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 + 128 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 * cos(radian * PI * b / (P * t)) ^ 6 - 128 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8) / (RAB ^ 2 - RBC ^ 2);
    const BX = (2 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 - RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) - RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) + 2 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 - RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) - RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) - 256 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 18 + 1152 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 16 - 2112 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 - 256 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 * cos(radian * PI * b / (P * t)) ^ 6 + 2016 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 + 384 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 * cos(radian * PI * b / (P * t)) ^ 6 - 1056 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 - 192 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 * cos(radian * PI * b / (P * t)) ^ 6 + 288 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 + 32 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 6 * cos(radian * PI * b / (P * t)) ^ 6 - 32 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 6) * sin(radian * 2 * PI * b / (P * t)) / (RAB ^ 2 - RBC ^ 2);
    const BY = (4 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 4 - 4 * RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 + RAB ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) - 2 * RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) * sin(radian * PI * b / (P * t)) ^ 2 + RAB ^ 2 * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) + 4 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 4 - 4 * RAB * RBC * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 - 2 * RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) * sin(radian * PI * b / (P * t)) ^ 2 + RAB * RBC * sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2) - RBC ^ 2 * (P * t / (2 * PI) - PLD + RAB - h) - 512 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 20 + 2560 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 18 - 5248 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 16 - 512 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 * cos(radian * PI * b / (P * t)) ^ 6 + 5632 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 14 + 1024 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 * cos(radian * PI * b / (P * t)) ^ 6 - 3328 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 12 - 640 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 * cos(radian * PI * b / (P * t)) ^ 6 + 1024 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 10 + 128 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8 * cos(radian * PI * b / (P * t)) ^ 6 - 128 * (P * t / (2 * PI) - PLD + RAB - h) ^ 3 * sin(radian * PI * b / (P * t)) ^ 8) / (RAB ^ 2 - RBC ^ 2);
    const ABX = 0 * meter;
    const ABY = P * t / (2 * PI) - PLD + RAB - h;
    const BCX = (-P * t / (2 * PI) + PLD - RAB + h + 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 - sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2)) * sin(radian * 2 * PI * b / (P * t));
    const BCY = (P * t / (2 * PI) - PLD + RAB - h - 2 * (P * t / (2 * PI) - PLD + RAB - h) * sin(radian * PI * b / (P * t)) ^ 2 + sqrt(RAB ^ 2 - 2 * RAB * RBC + RBC ^ 2 + 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 4 - 4 * (P * t / (2 * PI) - PLD + RAB - h) ^ 2 * sin(radian * PI * b / (P * t)) ^ 2)) * cos(radian * 2 * PI * b / (P * t));
    const CDX = (4 * BCX ^ 2 + 4 * BCY ^ 2 - 8 * BCY * (-BCX * sqrt(-(4 * BCX ^ 2 + 4 * BCY ^ 2 - P ^ 2 * t ^ 2 / PI ^ 2 + 4 * P * PLD * t / PI - 4 * P * RBC * t / PI - 4 * PLD ^ 2 + 8 * PLD * RBC - 4 * RBC ^ 2) * (4 * BCX ^ 2 + 4 * BCY ^ 2 - P ^ 2 * t ^ 2 / PI ^ 2 + 4 * P * PLD * t / PI + 4 * P * RBC * t / PI + 8 * P * RCD * t / PI - 4 * PLD ^ 2 - 8 * PLD * RBC - 16 * PLD * RCD - 4 * RBC ^ 2 - 16 * RBC * RCD - 16 * RCD ^ 2)) / (8 * (BCX ^ 2 + BCY ^ 2)) + BCY * (4 * BCX ^ 2 + 4 * BCY ^ 2 + P ^ 2 * t ^ 2 / PI ^ 2 - 4 * P * PLD * t / PI - 4 * P * RCD * t / PI + 4 * PLD ^ 2 + 8 * PLD * RCD - 4 * RBC ^ 2 - 8 * RBC * RCD) / (8 * (BCX ^ 2 + BCY ^ 2))) + P ^ 2 * t ^ 2 / PI ^ 2 - 4 * P * PLD * t / PI - 4 * P * RCD * t / PI + 4 * PLD ^ 2 + 8 * PLD * RCD - 4 * RBC ^ 2 - 8 * RBC * RCD) / (8 * BCX);
    const CDY = -BCX * sqrt(-(4 * BCX ^ 2 + 4 * BCY ^ 2 - P ^ 2 * t ^ 2 / PI ^ 2 + 4 * P * PLD * t / PI - 4 * P * RBC * t / PI - 4 * PLD ^ 2 + 8 * PLD * RBC - 4 * RBC ^ 2) * (4 * BCX ^ 2 + 4 * BCY ^ 2 - P ^ 2 * t ^ 2 / PI ^ 2 + 4 * P * PLD * t / PI + 4 * P * RBC * t / PI + 8 * P * RCD * t / PI - 4 * PLD ^ 2 - 8 * PLD * RBC - 16 * PLD * RCD - 4 * RBC ^ 2 - 16 * RBC * RCD - 16 * RCD ^ 2)) / (8 * (BCX ^ 2 + BCY ^ 2)) + BCY * (4 * BCX ^ 2 + 4 * BCY ^ 2 + P ^ 2 * t ^ 2 / PI ^ 2 - 4 * P * PLD * t / PI - 4 * P * RCD * t / PI + 4 * PLD ^ 2 + 8 * PLD * RCD - 4 * RBC ^ 2 - 8 * RBC * RCD) / (8 * (BCX ^ 2 + BCY ^ 2));
    const CX = CDX - RCD * (-BCX + CDX) / (RBC + RCD);
    const CY = CDY - RCD * (-BCY + CDY) / (RBC + RCD);
    const CDR = sqrt(CDX ^ 2 + CDY ^ 2);
    const DX = CDX * (CDR + RCD) / CDR;
    const DY = CDY * (CDR + RCD) / CDR;
    const TANG = -PI / t;
    const EX = -sqrt(DX ^ 2 + DY ^ 2) * sin(radian * 2 * TANG + asin(DX / sqrt(DX ^ 2 + DY ^ 2)));
    const EY = sqrt(DX ^ 2 + DY ^ 2) * cos(radian * 2 * TANG + asin(DX / sqrt(DX ^ 2 + DY ^ 2)));
    const FX = -sqrt(CX ^ 2 + CY ^ 2) * sin(radian * 2 * TANG + asin(CX / sqrt(CX ^ 2 + CY ^ 2)));
    const FY = sqrt(CX ^ 2 + CY ^ 2) * cos(radian * 2 * TANG + asin(CX / sqrt(CX ^ 2 + CY ^ 2)));
    const EFX = -sqrt(CDX ^ 2 + CDY ^ 2) * sin(radian * 2 * TANG + asin(CDX / sqrt(CDX ^ 2 + CDY ^ 2)));
    const EFY = sqrt(CDX ^ 2 + CDY ^ 2) * cos(radian * 2 * TANG + asin(CDX / sqrt(CDX ^ 2 + CDY ^ 2)));
    const GX = -sqrt(BX ^ 2 + BY ^ 2) * sin(radian * 2 * TANG + asin(BX / sqrt(BX ^ 2 + BY ^ 2)));
    const GY = sqrt(BX ^ 2 + BY ^ 2) * cos(radian * 2 * TANG + asin(BX / sqrt(BX ^ 2 + BY ^ 2)));
    const FGX = -sqrt(BCX ^ 2 + BCY ^ 2) * sin(radian * 2 * TANG + asin(BCX / sqrt(BCX ^ 2 + BCY ^ 2)));
    const FGY = sqrt(BCX ^ 2 + BCY ^ 2) * cos(radian * 2 * TANG + asin(BCX / sqrt(BCX ^ 2 + BCY ^ 2)));

    const A = vector(AX, AY);
    const B = vector(BX, BY);
    const AB = vector(ABX, ABY);
    const C = vector(CX, CY);
    const BC = vector(BCX, BCY);
    const D = vector(DX, DY);
    const CD = vector(CDX, CDY);
    const E = vector(EX, EY);
    const F = vector(FX, FY);
    const EF = vector(EFX, EFY);
    const FG = vector(FGX, FGY);
    const G = vector(GX, GY);

    // 弧中点（Onshape skArc 以起点/中点/终点定义圆弧）
    const ABM = getArcMidPointShorter(A, B, AB);
    const BCM = getArcMidPointShorter(B, C, BC);
    const CDM = getArcMidPointShorter(C, D, CD);
    const DEM = getArcMidPointShorter(D, E, vector(0, 0) * meter);
    const EFM = getArcMidPointShorter(E, F, EF);
    const FGM = getArcMidPointShorter(F, G, FG);

    return {
                "A" : A,
                "B" : B,
                "C" : C,
                "D" : D,
                "E" : E,
                "F" : F,
                "G" : G,
                "ABM" : ABM,
                "BCM" : BCM,
                "CDM" : CDM,
                "DEM" : DEM,
                "EFM" : EFM,
                "FGM" : FGM,
                "alpha" : alpha
            };
}

function getArcMidPointShorter(start is Vector, end is Vector, center is Vector)
{
    const radius = norm(start - center);
    const mid_vec = ((start - center) + (end - center)) / 2;
    const mv_radius = norm(mid_vec);
    return center + radius * mid_vec / mv_radius;
}

const FILL_BOUNDS =
{
            (meter) : [0, 0.02, 10],
            (centimeter) : 2,
            (millimeter) : 20,
            (inch) : 0.75
        } as LengthBoundSpec;

const PULLEY_COUNT_BOUNDS =
{
            (unitless) : [1, 2, 4]
        } as IntegerBoundSpec;

const SHAFT_DIA_BOUNDS =
{
            (millimeter) : [2, 10, 500],
            (inch) : 0.375
        } as LengthBoundSpec;

const OFFSET_BOUNDS =
{
            (millimeter) : [1, 12, 500],
            (inch) : 0.5
        } as LengthBoundSpec;

const TEETH_BOUNDS =
{
            (unitless) : [10, 28, 200]
        } as IntegerBoundSpec;

const WIDTH_BOUNDS =
{
            (millimeter) : [2, 6, 100],
            (inch) : 0.25
        } as LengthBoundSpec;

const HOLE_DEPTH_BOUNDS =
{
            (millimeter) : [0.1, 20, 1000],
            (inch) : 0.01
        } as LengthBoundSpec;

const HOLE_R_BOUNDS =
{
            (millimeter) : [1, 6.35, 100],
            (inch) : 0.05
        } as LengthBoundSpec;

const HOLE_OFF_BOUNDS =
{
            (millimeter) : [0, 0, 50],
            (inch) : 0
        } as LengthBoundSpec;

const CTC_BOUNDS =
{
            (millimeter) : [5, 20, 500],
            (inch) : 0.75
        } as LengthBoundSpec;

// 端面底领尺寸（固定）：圆柱段轴向厚度 1mm，锥体收拢段最厚 2mm
const COLLAR_CYL_LEN = 1 * millimeter;
const COLLAR_CONE_LEN = 2 * millimeter;
// 填充体伸出端面、伸入底领实心盘的重叠长度（保证布尔缝合的体积重叠）
const FILL_OVERLAP = 0.5 * millimeter;

const FLANGE_O_BOUNDS =
{
            (millimeter) : [0, 1, 20],
            (inch) : 0.04
        } as LengthBoundSpec;

export enum ToothProfile
{
    annotation { "Name" : "GT2 2M (2mm pitch)" }
    GT2_2M,
    annotation { "Name" : "GT2 3M (3mm pitch)" }
    GT2_3M,
    annotation { "Name" : "GT2 5M (5mm pitch)" }
    GT2_5M,
    annotation { "Name" : "GT2 8M (8mm pitch)" }
    GT2_8M,
    annotation { "Name" : "HTD 3M (3mm pitch)" }
    HTD_3M,
    annotation { "Name" : "HTD 5M (5mm pitch)" }
    HTD_5M
}

// FT = 带轮法兰厚度（轴向），FH = 法兰墙高出齿顶的高度 —— 与 COTS 标准
// 法兰 GT2 带轮一致（如 SDP / Gates 法兰带轮：法兰锥形升起超过齿顶并保持）
const ToothProfileDefinitions = {
        (ToothProfile.GT2_2M) : {
            "P" : 2 * millimeter,
            "R1" : 0.15 * millimeter,
            "R2" : 1 * millimeter,
            "R3" : 0.555 * millimeter,
            "b" : 0.4 * millimeter,
            "H" : 1.38 * millimeter,
            "h" : 0.75 * millimeter,
            "i" : 0.63 * millimeter,
            "PLD" : 0.254 * millimeter,
            "U" : 0.254 * millimeter,
            "FT" : 1 * millimeter,
            "FH" : 0.8 * millimeter
        },
        (ToothProfile.GT2_3M) : {
            "P" : 3 * millimeter,
            "R1" : 0.25 * millimeter,
            "R2" : 1.52 * millimeter,
            "R3" : 0.85 * millimeter,
            "b" : 0.61 * millimeter,
            "H" : 2.4 * millimeter,
            "h" : 1.14 * millimeter,
            "i" : 1.26 * millimeter,
            "PLD" : 0.381 * millimeter,
            "U" : 0.381 * millimeter,
            "FT" : 1.5 * millimeter,
            "FH" : 1.2 * millimeter
        },
        (ToothProfile.GT2_5M) : {
            "P" : 5 * millimeter,
            "R1" : 0.42 * millimeter,
            "R2" : 2.54 * millimeter,
            "R3" : 1.42 * millimeter,
            "b" : 1.02 * millimeter,
            "H" : 4 * millimeter,
            "h" : 1.9 * millimeter,
            "i" : 2.1 * millimeter,
            "PLD" : 0.5715 * millimeter,
            "U" : 0.5715 * millimeter,
            "FT" : 2 * millimeter,
            "FH" : 1.5 * millimeter
        },
        (ToothProfile.GT2_8M) : {
            "P" : 8 * millimeter,
            "R1" : 0.67 * millimeter,
            "R2" : 4.05 * millimeter,
            "R3" : 2.27 * millimeter,
            "b" : 1.63 * millimeter,
            "H" : 6.4 * millimeter,
            "h" : 3.04 * millimeter,
            "i" : 3.36 * millimeter,
            "PLD" : 0.6858 * millimeter,
            "U" : 0.6858 * millimeter,
            "FT" : 3 * millimeter,
            "FH" : 2 * millimeter
        },
        // HTD 齿形：节距 / 齿深 / PLD / U / 法兰为 HTD 标准；R1/R2/R3/b 取与
        // GT 解析公式几何约束自洽的比例（近似齿形，避免 sqrt 负值 NaN）
        (ToothProfile.HTD_3M) : {
            "P" : 3 * millimeter,
            "R1" : 0.3 * millimeter,
            "R2" : 1.52 * millimeter,
            "R3" : 0.85 * millimeter,
            "b" : 0.7 * millimeter,
            "H" : 2.4 * millimeter,
            "h" : 1.28 * millimeter,
            "i" : 1.4 * millimeter,
            "PLD" : 0.381 * millimeter,
            "U" : 0.381 * millimeter,
            "FT" : 1.5 * millimeter,
            "FH" : 1 * millimeter
        },
        (ToothProfile.HTD_5M) : {
            "P" : 5 * millimeter,
            "R1" : 0.5 * millimeter,
            "R2" : 2.53 * millimeter,
            "R3" : 1.42 * millimeter,
            "b" : 1.17 * millimeter,
            "H" : 4 * millimeter,
            "h" : 2.13 * millimeter,
            "i" : 2.33 * millimeter,
            "PLD" : 0.5715 * millimeter,
            "U" : 0.5715 * millimeter,
            "FT" : 2 * millimeter,
            "FH" : 1.2 * millimeter
        },
    };
