import * as d3 from "https://cdn.jsdelivr.net/npm/d3@7/+esm";

const colors = ["#55d6a9", "#69a8ff", "#f2bd5c", "#d68cff", "#ff837a", "#66d7e8"];
const flowMaps = new WeakMap();
const replicaFlowMaps = new WeakMap();
const workflowRunMaps = new WeakMap();
const sagaRunMaps = new WeakMap();
let workflowRunMapSequence = 0;
let sagaRunMapSequence = 0;

export function renderThroughputChart(element, rawPoints, compact = false) {
    const points = rawPoints.map(point => ({
        application: point.applicationName,
        timestamp: new Date(point.timestampUtc),
        value: point.consumedPerSecond
    }));
    const series = d3.groups(points, point => point.application)
        .sort(([left], [right]) => d3.ascending(left, right));

    const root = d3.select(element);
    root.selectAll("*").remove();
    if (series.length === 0) {
        root.append("p").attr("class", "panel-empty").text("Waiting for throughput samples.");
        return;
    }

    const legend = root.append("div").attr("class", "chart-legend");
    series.forEach(([name, values], index) => {
        const item = legend.append("span");
        item.append("i").style("background", colors[index % colors.length]);
        item.append("span").text(name);
        item.append("strong").text(`${d3.format(".2f")(values.at(-1).value)} / sec`);
    });

    const width = 800;
    const height = compact ? 170 : 230;
    const margin = { top: 12, right: 16, bottom: 32, left: 46 };
    const timestamps = points.map(point => point.timestamp);
    const x = d3.scaleUtc()
        .domain(d3.extent(timestamps))
        .range([margin.left, width - margin.right]);
    const y = d3.scaleLinear()
        .domain([0, Math.max(1, d3.max(points, point => point.value))])
        .nice()
        .range([height - margin.bottom, margin.top]);
    const line = d3.line()
        .x(point => x(point.timestamp))
        .y(point => y(point.value))
        .curve(d3.curveMonotoneX);

    const svg = root.append("svg")
        .attr("viewBox", `0 0 ${width} ${height}`)
        .attr("role", "img")
        .attr("aria-label", "Consumed messages per second by application");

    svg.append("g")
        .attr("class", "chart-grid")
        .attr("transform", `translate(${margin.left},0)`)
        .call(d3.axisLeft(y).ticks(compact ? 3 : 4).tickSize(-(width - margin.left - margin.right)).tickFormat(""));
    svg.append("g")
        .attr("class", "chart-axis")
        .attr("transform", `translate(0,${height - margin.bottom})`)
        .call(d3.axisBottom(x).ticks(compact ? 3 : 5).tickFormat(d3.utcFormat("%H:%M:%S")));
    svg.append("g")
        .attr("class", "chart-axis")
        .attr("transform", `translate(${margin.left},0)`)
        .call(d3.axisLeft(y).ticks(compact ? 3 : 4));

    series.forEach(([, values], index) => {
        svg.append("path")
            .datum(values)
            .attr("class", "chart-line")
            .attr("stroke", colors[index % colors.length])
            .attr("d", line);
    });
}

export function initializeFlowMap(element) {
    disposeFlowMap(element);
    flowMaps.set(element, new FlowMap(element));
}

export function updateFlowMap(element, graph) {
    flowMaps.get(element)?.update(graph);
}

export function disposeFlowMap(element) {
    const map = flowMaps.get(element);
    if (map) {
        map.dispose();
        flowMaps.delete(element);
    }
}

class FlowMap {
    constructor(element) {
        this.element = element;
        this.nodes = [];
        this.links = [];
        this.selectedNode = null;
        this.svg = d3.select(element).select(".flow-map-canvas");
        this.stage = this.svg.append("g").attr("class", "flow-map-stage");
        this.linkGroup = this.stage.append("g").attr("class", "flow-map-links");
        this.labelGroup = this.stage.append("g").attr("class", "flow-map-labels");
        this.nodeGroup = this.stage.append("g").attr("class", "flow-map-nodes");

        const definitions = this.svg.append("defs");
        definitions.append("marker")
            .attr("id", "flow-arrow")
            .attr("viewBox", "0 -5 10 10")
            .attr("refX", 10)
            .attr("refY", 0)
            .attr("markerWidth", 7)
            .attr("markerHeight", 7)
            .attr("markerUnits", "userSpaceOnUse")
            .attr("orient", "auto")
            .append("path")
            .attr("d", "M0,-5L10,0L0,5")
            .attr("class", "flow-map-arrow");

        this.zoom = d3.zoom()
            .scaleExtent([0.35, 3.5])
            .on("zoom", event => this.stage.attr("transform", event.transform));
        this.svg.call(this.zoom);
        this.linkForce = d3.forceLink().id(node => node.id).distance(275).strength(0.5);
        this.simulation = d3.forceSimulation()
            .force("link", this.linkForce)
            .force("charge", d3.forceManyBody().strength(-780))
            .force("collide", d3.forceCollide(125).iterations(6))
            .force("x", d3.forceX().strength(0.09))
            .force("y", d3.forceY().strength(0.13))
            .on("tick", () => this.onTick());
        this.drag = d3.drag()
            .on("start", event => {
                if (!event.active) this.simulation.alphaTarget(0.18).restart();
                event.subject.fx = event.subject.x;
                event.subject.fy = event.subject.y;
            })
            .on("drag", event => {
                event.subject.fx = event.x;
                event.subject.fy = event.y;
            })
            .on("end", event => {
                if (!event.active) this.simulation.alphaTarget(0);
                event.subject.fx = null;
                event.subject.fy = null;
            });

        this.registerControls();
        this.resizeObserver = new ResizeObserver(() => this.resize());
        this.resizeObserver.observe(element);
        this.resize();
    }

    registerControls() {
        this.element.querySelector('[data-flow-map-action="zoom-in"]')
            ?.addEventListener("click", this.zoomIn = () => this.svg.transition().duration(160).call(this.zoom.scaleBy, 1.35));
        this.element.querySelector('[data-flow-map-action="zoom-out"]')
            ?.addEventListener("click", this.zoomOut = () => this.svg.transition().duration(160).call(this.zoom.scaleBy, 1 / 1.35));
        this.element.querySelector('[data-flow-map-action="reset"]')
            ?.addEventListener("click", this.reset = () => this.svg.transition().duration(180).call(this.zoom.transform, d3.zoomIdentity));
    }

    resize() {
        const width = Math.max(this.element.clientWidth, 320);
        const height = Math.max(this.element.clientHeight, 420);
        this.svg.attr("viewBox", `${-width / 2} ${-height / 2} ${width} ${height}`);
    }

    update(graph) {
        const rawNodes = graph?.nodes || [];
        const rawLinks = graph?.links || [];
        const previousNodes = new Map(this.nodes.map(node => [node.id, node]));
        const nextNodeIds = new Set(rawNodes.map(node => node.id));
        const groupedLinks = d3.rollups(
            rawLinks.filter(link => nextNodeIds.has(link.source) && nextNodeIds.has(link.target)),
            links => ({
                count: d3.sum(links, link => link.count),
                messageTypes: new Set(links.map(link => link.messageType).filter(Boolean)).size
            }),
            link => link.source,
            link => link.target)
            .flatMap(([source, targets]) => targets.map(([target, summary]) => ({
                id: `${source}->${target}`,
                source,
                target,
                ...summary
            })));
        const structureChanged = this.hasStructureChanged(rawNodes, groupedLinks);

        this.nodes = rawNodes.map(node => ({ ...previousNodes.get(node.id), ...node }));
        this.links = groupedLinks;
        this.windowSeconds = Math.max(1, graph?.windowSeconds || 300);
        this.renderLinks();
        this.renderNodes();
        this.simulation.nodes(this.nodes);
        this.linkForce.links(this.links);

        if (structureChanged) {
            this.simulation.stop().alpha(1);
            for (let index = 0; index < 180; index++) this.simulation.tick();
        }
        this.simulation.alpha(structureChanged ? 0.55 : 0.15).restart();
        this.updateHighlights();
    }

    hasStructureChanged(nodes, links) {
        if (nodes.length !== this.nodes.length || links.length !== this.links.length) return true;
        const nodeIds = new Set(this.nodes.map(node => node.id));
        const linkIds = new Set(this.links.map(link => link.id));
        return nodes.some(node => !nodeIds.has(node.id)) || links.some(link => !linkIds.has(link.id));
    }

    renderLinks() {
        const maximumCount = Math.max(1, d3.max(this.links, link => link.count) ?? 1);
        const linkWidth = d3.scaleSqrt().domain([0, maximumCount]).range([1.5, 7]);
        this.linkElements = this.linkGroup.selectAll("path")
            .data(this.links, link => link.id)
            .join(
                enter => enter.append("path").attr("opacity", 0)
                    .call(selection => selection.transition().duration(140).attr("opacity", 1)),
                update => update,
                exit => exit.transition().duration(140).attr("opacity", 0).remove())
            .attr("class", "flow-map-link")
            .attr("marker-end", "url(#flow-arrow)")
            .style("stroke-width", link => `${linkWidth(link.count)}px`);

        this.labelElements = this.labelGroup.selectAll("g")
            .data(this.links, link => link.id)
            .join(
                enter => {
                    const label = enter.append("g").attr("class", "flow-map-label");
                    label.append("rect").attr("x", -43).attr("y", -13).attr("width", 86).attr("height", 26).attr("rx", 13);
                    label.append("text").attr("text-anchor", "middle").attr("dominant-baseline", "central");
                    label.append("title");
                    return label;
                },
                update => update,
                exit => exit.remove());
        this.labelElements.select("text").text(link => `${formatFlowRate(link.count / this.windowSeconds)}/s`);
        this.labelElements.select("title").text(link =>
            `${link.count.toLocaleString()} observations · ${link.messageTypes} message type${link.messageTypes === 1 ? "" : "s"}`);
    }

    renderNodes() {
        this.nodeElements = this.nodeGroup.selectAll("g")
            .data(this.nodes, node => node.id)
            .join(
                enter => {
                    const node = enter.append("g")
                        .attr("class", "flow-map-node")
                        .attr("opacity", 0)
                        .call(this.drag)
                        .on("click", (_event, value) => {
                            this.selectedNode = this.selectedNode?.id === value.id ? null : value;
                            this.updateHighlights();
                        })
                        .on("mouseover", (_event, value) => {
                            this.hoveredNode = value;
                            this.updateHighlights();
                        })
                        .on("mouseout", () => {
                            this.hoveredNode = null;
                            this.updateHighlights();
                        });
                    node.append("rect").attr("x", -96).attr("y", -38).attr("width", 192).attr("height", 76).attr("rx", 13);
                    node.append("circle").attr("cx", -72).attr("r", 6);
                    node.append("text").attr("x", -56).attr("y", -5).attr("class", "flow-map-name");
                    node.append("text").attr("x", -56).attr("y", 17).attr("class", "flow-map-meta");
                    node.append("title");
                    node.transition().duration(140).attr("opacity", 1);
                    return node;
                },
                update => update,
                exit => exit.transition().duration(140).attr("opacity", 0).remove());
        this.nodeElements.select("circle").attr("class", node => node.onlineInstances > 0 ? "online" : "offline");
        this.nodeElements.select(".flow-map-name").text(node => trimText(node.label, 23));
        this.nodeElements.select(".flow-map-meta").text(node => `${node.onlineInstances} / ${node.totalInstances} replicas online`);
        this.nodeElements.select("title").text(node => `${node.label}\n${node.onlineInstances} of ${node.totalInstances} replicas online`);
    }

    updateHighlights() {
        const activeNode = this.hoveredNode || this.selectedNode;
        const neighbors = activeNode ? new Set(this.getNeighborIds(activeNode.id)) : null;
        this.nodeElements
            ?.classed("selected", node => this.selectedNode?.id === node.id)
            .classed("related", node => neighbors?.has(node.id) === true)
            .classed("dimmed", node => neighbors !== null && !neighbors.has(node.id));
        this.linkElements?.classed("dimmed", link => activeNode && !this.isNeighborLink(activeNode.id, link));
        this.labelElements?.classed("dimmed", link => activeNode && !this.isNeighborLink(activeNode.id, link));
    }

    getNeighborIds(nodeId) {
        const neighbors = [nodeId];
        for (const link of this.links) {
            const source = getNodeId(link.source);
            const target = getNodeId(link.target);
            if (source === nodeId) neighbors.push(target);
            if (target === nodeId) neighbors.push(source);
        }
        return neighbors;
    }

    isNeighborLink(nodeId, link) {
        return getNodeId(link.source) === nodeId || getNodeId(link.target) === nodeId;
    }

    onTick() {
        this.nodeElements?.attr("transform", node => `translate(${node.x},${node.y})`);
        this.linkElements?.attr("d", edgePath);
        this.labelElements?.attr("transform", link => {
            const midpoint = edgeMidpoint(link);
            return `translate(${midpoint.x},${midpoint.y})`;
        });
    }

    dispose() {
        this.simulation.stop();
        this.resizeObserver?.disconnect();
        this.element.querySelector('[data-flow-map-action="zoom-in"]')?.removeEventListener("click", this.zoomIn);
        this.element.querySelector('[data-flow-map-action="zoom-out"]')?.removeEventListener("click", this.zoomOut);
        this.element.querySelector('[data-flow-map-action="reset"]')?.removeEventListener("click", this.reset);
        this.svg.on(".zoom", null);
        this.svg.selectAll("*").remove();
    }
}

export function initializeReplicaFlowMap(element) {
    disposeReplicaFlowMap(element);
    replicaFlowMaps.set(element, new ReplicaFlowMap(element));
}

export function updateReplicaFlowMap(element, graph) {
    replicaFlowMaps.get(element)?.update(graph);
}

export function disposeReplicaFlowMap(element) {
    const map = replicaFlowMaps.get(element);
    if (map) {
        map.dispose();
        replicaFlowMaps.delete(element);
    }
}

class ReplicaFlowMap {
    constructor(element) {
        this.element = element;
        this.nodes = [];
        this.anchors = [];
        this.links = [];
        this.groups = [];
        this.svg = d3.select(element).select(".replica-flow-map-canvas");
        this.stage = this.svg.append("g").attr("class", "replica-flow-map-stage");
        this.groupLayer = this.stage.append("g").attr("class", "replica-flow-groups");
        this.linkLayer = this.stage.append("g").attr("class", "replica-flow-links");
        this.labelLayer = this.stage.append("g").attr("class", "replica-flow-labels");
        this.nodeLayer = this.stage.append("g").attr("class", "replica-flow-nodes");

        const definitions = this.svg.append("defs");
        definitions.append("marker")
            .attr("id", "replica-flow-arrow")
            .attr("viewBox", "0 -5 10 10")
            .attr("refX", 10)
            .attr("refY", 0)
            .attr("markerWidth", 7)
            .attr("markerHeight", 7)
            .attr("markerUnits", "userSpaceOnUse")
            .attr("orient", "auto")
            .append("path")
            .attr("d", "M0,-5L10,0L0,5")
            .attr("class", "flow-map-arrow");

        this.zoom = d3.zoom()
            .scaleExtent([0.25, 4])
            .on("zoom", event => this.stage.attr("transform", event.transform));
        this.svg.call(this.zoom);
        this.linkForce = d3.forceLink()
            .id(node => node.id)
            .distance(link => link.internal ? 115 : 260)
            .strength(link => link.internal ? 0.9 : 0.48);
        this.simulation = d3.forceSimulation()
            .force("link", this.linkForce)
            .force("charge", d3.forceManyBody().strength(node => node.kind === "anchor" ? -1050 : -340))
            .force("collide", d3.forceCollide(node => node.kind === "anchor" ? 145 : 105).iterations(7))
            .force("x", d3.forceX().strength(0.035))
            .force("y", d3.forceY().strength(0.05))
            .on("tick", () => this.onTick());
        this.drag = d3.drag()
            .on("start", event => {
                if (!event.active) this.simulation.alphaTarget(0.16).restart();
                event.subject.fx = event.subject.x;
                event.subject.fy = event.subject.y;
            })
            .on("drag", event => {
                event.subject.fx = event.x;
                event.subject.fy = event.y;
            })
            .on("end", event => {
                if (!event.active) this.simulation.alphaTarget(0);
                event.subject.fx = null;
                event.subject.fy = null;
            });

        this.registerControls();
        this.resizeObserver = new ResizeObserver(() => this.resize());
        this.resizeObserver.observe(element);
        this.resize();
    }

    registerControls() {
        this.element.querySelector('[data-replica-flow-action="zoom-in"]')
            ?.addEventListener("click", this.zoomIn = () => this.svg.transition().duration(160).call(this.zoom.scaleBy, 1.35));
        this.element.querySelector('[data-replica-flow-action="zoom-out"]')
            ?.addEventListener("click", this.zoomOut = () => this.svg.transition().duration(160).call(this.zoom.scaleBy, 1 / 1.35));
        this.element.querySelector('[data-replica-flow-action="reset"]')
            ?.addEventListener("click", this.reset = () => this.fitToContent(true));
    }

    resize() {
        const width = Math.max(this.element.clientWidth, 320);
        const height = Math.max(this.element.clientHeight, 520);
        this.svg.attr("viewBox", `${-width / 2} ${-height / 2} ${width} ${height}`);
    }

    update(graph) {
        const rawNodes = graph?.nodes || [];
        const nodeIds = new Set(rawNodes.map(node => node.id));
        const rawLinks = (graph?.links || [])
            .filter(link => nodeIds.has(link.source) && nodeIds.has(link.target));
        const groupedLinks = d3.rollups(
            rawLinks,
            links => ({
                count: d3.sum(links, link => link.count),
                messageTypes: new Set(links.map(link => link.messageType).filter(Boolean)).size,
                endpoints: new Set(links.map(link => link.endpointName).filter(Boolean)).size
            }),
            link => link.source,
            link => link.target)
            .flatMap(([source, targets]) => targets.map(([target, summary]) => ({
                id: `${source}->${target}`,
                source,
                target,
                ...summary
            })));
        const applications = [...new Set(rawNodes.map(node => node.application))].sort(d3.ascending);
        const structureChanged = this.hasStructureChanged(rawNodes, groupedLinks, applications);
        const previousNodes = new Map(this.nodes.map(node => [node.id, node]));
        const previousAnchors = new Map(this.anchors.map(anchor => [anchor.application, anchor]));
        const radius = Math.max(230, applications.length * 78);

        this.anchors = applications.map((application, index) => {
            const existing = previousAnchors.get(application);
            const angle = applications.length === 1 ? 0 : index / applications.length * Math.PI * 2 - Math.PI / 2;
            return {
                ...existing,
                id: `application:${application}`,
                application,
                kind: "anchor",
                x: existing?.x ?? (applications.length === 1 ? 0 : Math.cos(angle) * radius),
                y: existing?.y ?? (applications.length === 1 ? 0 : Math.sin(angle) * radius)
            };
        });
        const anchors = new Map(this.anchors.map(anchor => [anchor.application, anchor]));
        this.nodes = rawNodes.map((node, index) => {
            const existing = previousNodes.get(node.id);
            const anchor = anchors.get(node.application);
            return {
                ...existing,
                ...node,
                kind: "replica",
                x: existing?.x ?? anchor.x + ((index % 3) - 1) * 34,
                y: existing?.y ?? anchor.y + (Math.floor(index / 3) % 3 - 1) * 34
            };
        });
        this.links = groupedLinks;
        this.windowSeconds = Math.max(1, graph?.windowSeconds || 300);
        const membersByApplication = d3.group(this.nodes, node => node.application);
        this.groups = applications.map(application => ({
            id: application,
            application,
            anchor: anchors.get(application),
            members: membersByApplication.get(application) || [],
            isFocus: (membersByApplication.get(application) || []).some(node => node.isFocus)
        }));
        const internalLinks = this.nodes.map(node => ({
            id: `internal:${node.id}`,
            source: `application:${node.application}`,
            target: node.id,
            internal: true
        }));

        this.renderGroups();
        this.renderLinks();
        this.renderNodes();
        this.simulation.nodes([...this.anchors, ...this.nodes]);
        this.linkForce.links([...internalLinks, ...this.links]);
        if (structureChanged) {
            this.simulation.stop().alpha(1);
            for (let index = 0; index < 220; index++) this.simulation.tick();
            this.fitToContent(false);
        }
        this.simulation.alpha(structureChanged ? 0.55 : 0.14).restart();
        this.updateHighlights();
    }

    hasStructureChanged(nodes, links, applications) {
        if (nodes.length !== this.nodes.length || links.length !== this.links.length || applications.length !== this.groups.length) return true;
        const nodeIds = new Set(this.nodes.map(node => node.id));
        const linkIds = new Set(this.links.map(link => link.id));
        const groupIds = new Set(this.groups.map(group => group.id));
        return nodes.some(node => !nodeIds.has(node.id))
            || links.some(link => !linkIds.has(link.id))
            || applications.some(application => !groupIds.has(application));
    }

    renderGroups() {
        this.groupElements = this.groupLayer.selectAll("g")
            .data(this.groups, group => group.id)
            .join(
                enter => {
                    const group = enter.append("g").attr("class", "replica-flow-group");
                    group.append("rect").attr("rx", 18);
                    group.append("text").attr("class", "replica-flow-group-name");
                    group.append("text").attr("class", "replica-flow-group-meta");
                    return group;
                },
                update => update,
                exit => exit.remove())
            .classed("focus", group => group.isFocus);
        this.groupElements.select(".replica-flow-group-name").text(group => trimText(group.application, 34));
        this.groupElements.select(".replica-flow-group-meta")
            .text(group => `${group.members.length} replica${group.members.length === 1 ? "" : "s"}`);
    }

    renderLinks() {
        const maximumCount = Math.max(1, d3.max(this.links, link => link.count) ?? 1);
        const linkWidth = d3.scaleSqrt().domain([0, maximumCount]).range([1.5, 7]);
        this.linkElements = this.linkLayer.selectAll("path")
            .data(this.links, link => link.id)
            .join("path")
            .attr("class", "flow-map-link replica-flow-link")
            .attr("marker-end", "url(#replica-flow-arrow)")
            .style("stroke-width", link => `${linkWidth(link.count)}px`);
        this.labelElements = this.labelLayer.selectAll("g")
            .data(this.links, link => link.id)
            .join(
                enter => {
                    const label = enter.append("g").attr("class", "flow-map-label replica-flow-label");
                    label.append("rect").attr("x", -43).attr("y", -13).attr("width", 86).attr("height", 26).attr("rx", 13);
                    label.append("text").attr("text-anchor", "middle").attr("dominant-baseline", "central");
                    label.append("title");
                    return label;
                },
                update => update,
                exit => exit.remove());
        this.labelElements.select("text").text(link => `${formatFlowRate(link.count / this.windowSeconds)}/s`);
        this.labelElements.select("title").text(link =>
            `${link.count.toLocaleString()} observations · ${link.messageTypes} message type${link.messageTypes === 1 ? "" : "s"} · ${link.endpoints} endpoint${link.endpoints === 1 ? "" : "s"}`);
    }

    renderNodes() {
        this.nodeElements = this.nodeLayer.selectAll("g")
            .data(this.nodes, node => node.id)
            .join(
                enter => {
                    const node = enter.append("g")
                        .attr("class", "flow-map-node replica-flow-node")
                        .call(this.drag)
                        .on("click", (_event, value) => {
                            this.selectedNode = this.selectedNode?.id === value.id ? null : value;
                            this.updateHighlights();
                        })
                        .on("mouseover", (_event, value) => {
                            this.hoveredNode = value;
                            this.updateHighlights();
                        })
                        .on("mouseout", () => {
                            this.hoveredNode = null;
                            this.updateHighlights();
                        });
                    node.append("rect").attr("x", -85).attr("y", -29).attr("width", 170).attr("height", 58).attr("rx", 11);
                    node.append("circle").attr("cx", -63).attr("r", 5);
                    node.append("text").attr("x", -49).attr("y", -3).attr("class", "flow-map-name");
                    node.append("text").attr("x", -49).attr("y", 15).attr("class", "flow-map-meta");
                    node.append("title");
                    return node;
                },
                update => update,
                exit => exit.remove());
        this.nodeElements.select("circle").attr("class", node => node.online ? "online" : "offline");
        this.nodeElements.select(".flow-map-name").text(node => trimText(node.instanceId, 20));
        this.nodeElements.select(".flow-map-meta").text(node => trimText(node.busId, 24));
        this.nodeElements.select("title").text(node => `${node.application}\nReplica ${node.instanceId}\nBus ${node.busId}\n${node.online ? "Online" : "Offline"}`);
    }

    updateHighlights() {
        const activeNode = this.hoveredNode || this.selectedNode;
        const neighbors = activeNode ? new Set(this.getNeighborIds(activeNode.id)) : null;
        this.nodeElements
            ?.classed("selected", node => this.selectedNode?.id === node.id)
            .classed("related", node => neighbors?.has(node.id) === true)
            .classed("dimmed", node => neighbors !== null && !neighbors.has(node.id));
        this.linkElements?.classed("dimmed", link => activeNode && !this.isNeighborLink(activeNode.id, link));
        this.labelElements?.classed("dimmed", link => activeNode && !this.isNeighborLink(activeNode.id, link));
        this.groupElements?.classed("dimmed", group => activeNode && group.application !== activeNode.application
            && !group.members.some(node => neighbors?.has(node.id)));
    }

    getNeighborIds(nodeId) {
        const neighbors = [nodeId];
        for (const link of this.links) {
            const source = getNodeId(link.source);
            const target = getNodeId(link.target);
            if (source === nodeId) neighbors.push(target);
            if (target === nodeId) neighbors.push(source);
        }
        return neighbors;
    }

    isNeighborLink(nodeId, link) {
        return getNodeId(link.source) === nodeId || getNodeId(link.target) === nodeId;
    }

    onTick() {
        this.nodeElements?.attr("transform", node => `translate(${node.x},${node.y})`);
        this.linkElements?.attr("d", replicaEdgePath);
        this.labelElements?.attr("transform", link => {
            const midpoint = edgeMidpoint(link);
            return `translate(${midpoint.x},${midpoint.y})`;
        });
        this.groupElements?.each(function (group) {
            const points = [...group.members, group.anchor].filter(point => Number.isFinite(point.x) && Number.isFinite(point.y));
            const minimumX = d3.min(points, point => point.x) - 112;
            const maximumX = d3.max(points, point => point.x) + 112;
            const minimumY = d3.min(points, point => point.y) - 62;
            const maximumY = d3.max(points, point => point.y) + 58;
            const element = d3.select(this);
            element.select("rect")
                .attr("x", minimumX)
                .attr("y", minimumY)
                .attr("width", maximumX - minimumX)
                .attr("height", maximumY - minimumY);
            element.select(".replica-flow-group-name").attr("x", minimumX + 18).attr("y", minimumY + 24);
            element.select(".replica-flow-group-meta").attr("x", maximumX - 18).attr("y", minimumY + 24);
        });
    }

    fitToContent(animate) {
        const stage = this.stage.node();
        if (!stage || this.nodes.length === 0) return;
        const bounds = stage.getBBox();
        if (bounds.width <= 0 || bounds.height <= 0) return;
        const width = Math.max(this.element.clientWidth, 320);
        const height = Math.max(this.element.clientHeight, 520);
        const scale = Math.min(1.15, width / (bounds.width + 90), height / (bounds.height + 90));
        const centerX = bounds.x + bounds.width / 2;
        const centerY = bounds.y + bounds.height / 2;
        const transform = d3.zoomIdentity
            .translate(-centerX * scale, -centerY * scale)
            .scale(scale);
        const selection = animate ? this.svg.transition().duration(180) : this.svg;
        selection.call(this.zoom.transform, transform);
    }

    dispose() {
        this.simulation.stop();
        this.resizeObserver?.disconnect();
        this.element.querySelector('[data-replica-flow-action="zoom-in"]')?.removeEventListener("click", this.zoomIn);
        this.element.querySelector('[data-replica-flow-action="zoom-out"]')?.removeEventListener("click", this.zoomOut);
        this.element.querySelector('[data-replica-flow-action="reset"]')?.removeEventListener("click", this.reset);
        this.svg.on(".zoom", null);
        this.svg.selectAll("*").remove();
    }
}

export function initializeSagaRunMap(element) {
    disposeSagaRunMap(element);
    sagaRunMaps.set(element, new SagaRunMap(element));
}

export function updateSagaRunMap(element, graph) {
    sagaRunMaps.get(element)?.update(graph);
}

export function zoomSagaRunMap(element, factor) {
    sagaRunMaps.get(element)?.zoomBy(factor);
}

export function resetSagaRunMap(element) {
    sagaRunMaps.get(element)?.fitToContent(true);
}

export function disposeSagaRunMap(element) {
    const map = sagaRunMaps.get(element);
    if (map) {
        map.dispose();
        sagaRunMaps.delete(element);
    }
}

class SagaRunMap {
    constructor(element) {
        this.element = element;
        this.svg = d3.select(element).select(".saga-run-map-canvas");
        this.stage = this.svg.append("g").attr("class", "saga-run-map-stage");
        this.linkGroup = this.stage.append("g");
        this.labelGroup = this.stage.append("g");
        this.failureGroup = this.stage.append("g");
        this.nodeGroup = this.stage.append("g");
        this.markerId = `saga-run-arrow-${++sagaRunMapSequence}`;
        this.observedMarkerId = `${this.markerId}-observed`;
        const definitions = this.svg.append("defs");
        this.appendMarker(definitions, this.markerId, "saga-run-map-arrow");
        this.appendMarker(definitions, this.observedMarkerId, "saga-run-map-arrow observed");
        this.zoom = d3.zoom()
            .scaleExtent([0.35, 3.5])
            .on("zoom", event => this.stage.attr("transform", event.transform));
        this.svg.call(this.zoom);
        this.structureKey = null;
    }

    appendMarker(definitions, id, className) {
        definitions.append("marker")
            .attr("id", id)
            .attr("viewBox", "0 -5 10 10")
            .attr("refX", 9)
            .attr("refY", 0)
            .attr("markerWidth", 7)
            .attr("markerHeight", 7)
            .attr("markerUnits", "userSpaceOnUse")
            .attr("orient", "auto")
            .append("path")
            .attr("d", "M0,-5L10,0L0,5")
            .attr("class", className);
    }

    update(graph) {
        const nodes = (graph?.nodes || []).map(node => ({ ...node, level: null }));
        const nodeById = new Map(nodes.map(node => [node.id, node]));
        const links = (graph?.links || [])
            .filter(link => nodeById.has(link.source) && nodeById.has(link.target))
            .map((link, index) => ({ ...link, id: `${link.source}:${link.event}:${link.target}:${index}` }));
        const outgoing = d3.group(links.filter(link => link.source !== link.target), link => link.source);
        const initial = nodeById.get("Initial") || nodes[0];
        if (initial) initial.level = 0;
        const queue = initial ? [initial] : [];
        while (queue.length) {
            const source = queue.shift();
            for (const link of outgoing.get(source.id) || []) {
                const target = nodeById.get(link.target);
                if (target.level !== null) continue;
                target.level = source.level + 1;
                queue.push(target);
            }
        }
        let fallbackLevel = d3.max(nodes, node => node.level ?? 0) || 0;
        nodes.filter(node => node.level === null).forEach(node => node.level = ++fallbackLevel);
        const finalNode = nodeById.get("Final");
        if (finalNode) finalNode.level = Math.max(finalNode.level, d3.max(nodes, node => node.level) || 0);

        const occupied = new Map();
        nodes.sort((left, right) => d3.ascending(left.sequence, right.sequence)).forEach(node => {
            const offset = occupied.get(node.level) || 0;
            occupied.set(node.level, offset + 1);
            node.x = 150 + node.level * 305;
            node.y = 105 + offset * 132;
        });
        links.forEach(link => {
            link.sourceNode = nodeById.get(link.source);
            link.targetNode = nodeById.get(link.target);
        });
        const failures = (graph?.failedDeliveries || [])
            .filter(delivery => nodeById.has(delivery.state))
            .map((delivery, index) => ({ ...delivery, id: `${delivery.state}:${delivery.event}:${index}`, node: nodeById.get(delivery.state) }));
        const width = Math.max(650, (d3.max(nodes, node => node.level) + 1) * 305);
        const height = Math.max(310, (d3.max(occupied.values()) || 1) * 132 + 92);
        const structureKey = JSON.stringify({ nodes: nodes.map(node => node.id), links: links.map(link => link.id), failures: failures.map(failure => failure.id) });
        const structureChanged = structureKey !== this.structureKey;
        this.structureKey = structureKey;
        this.svg.attr("viewBox", `0 0 ${width} ${height}`);
        this.element.style.minHeight = `${Math.min(500, Math.max(340, height))}px`;
        this.renderLinks(links);
        this.renderFailures(failures);
        this.renderNodes(nodes);
        if (structureChanged) queueMicrotask(() => this.fitToContent(false));
    }

    renderLinks(links) {
        this.linkGroup.selectAll("path")
            .data(links, link => link.id)
            .join("path")
            .attr("class", link => `saga-run-map-link${link.observedCount > 0 ? " observed" : ""}${link.ignored ? " ignored" : ""}`)
            .attr("marker-end", link => `url(#${link.observedCount > 0 ? this.observedMarkerId : this.markerId})`)
            .attr("d", link => this.linkPath(link));
        this.labelGroup.selectAll("text")
            .data(links, link => link.id)
            .join("text")
            .attr("class", link => `saga-run-map-link-label${link.observedCount > 0 ? " observed" : ""}`)
            .attr("x", link => link.source === link.target ? link.sourceNode.x : (link.sourceNode.x + link.targetNode.x) / 2)
            .attr("y", link => link.source === link.target ? link.sourceNode.y - 83 : (link.sourceNode.y + link.targetNode.y) / 2 - 10)
            .text(link => `${trimText(link.event, 24)}${link.observedCount > 0 ? ` · ${link.observedCount} × · ${formatMilliseconds(link.durationMs)}` : ""}`);
    }

    renderFailures(failures) {
        const selection = this.failureGroup.selectAll("g")
            .data(failures, failure => failure.id)
            .join(enter => {
                const group = enter.append("g").attr("class", "saga-run-map-failure");
                group.append("path");
                group.append("text");
                return group;
            });
        selection.select("path")
            .attr("d", failure => `M${failure.node.x},${failure.node.y + 42} v48 h72`);
        selection.select("text")
            .attr("x", failure => failure.node.x + 78)
            .attr("y", failure => failure.node.y + 94)
            .text(failure => `${trimText(failure.event, 22)} failed · ${formatMilliseconds(failure.durationMs)}`);
    }

    renderNodes(nodes) {
        const selection = this.nodeGroup.selectAll("g")
            .data(nodes, node => node.id)
            .join(enter => {
                const group = enter.append("g").attr("class", "saga-run-map-state");
                group.append("rect").attr("x", -100).attr("y", -42).attr("width", 200).attr("height", 84).attr("rx", 42);
                group.append("text").attr("class", "state-kind").attr("x", -76).attr("y", -17);
                group.append("text").attr("class", "state-name").attr("y", 5);
                group.append("text").attr("class", "state-status").attr("y", 25);
                return group;
            });
        selection
            .attr("class", node => `saga-run-map-state${node.visited ? " observed" : ""}${node.current ? " current" : ""}`)
            .attr("transform", node => `translate(${node.x},${node.y})`);
        selection.select(".state-kind").text(node => node.terminal ? "Final state" : node.id === "Initial" ? "Initial state" : "State");
        selection.select(".state-name").text(node => trimText(node.id, 25));
        selection.select(".state-status").text(node => node.current ? "Current committed state" : node.visited ? "Visited" : "Not visited");
    }

    linkPath(link) {
        if (link.source === link.target)
            return `M${link.sourceNode.x + 54},${link.sourceNode.y - 35} C${link.sourceNode.x + 125},${link.sourceNode.y - 110} ${link.sourceNode.x - 125},${link.sourceNode.y - 110} ${link.sourceNode.x - 54},${link.sourceNode.y - 35}`;
        const startX = link.sourceNode.x + 100;
        const endX = link.targetNode.x - 100;
        const middleX = (startX + endX) / 2;
        return `M${startX},${link.sourceNode.y} C${middleX},${link.sourceNode.y} ${middleX},${link.targetNode.y} ${endX},${link.targetNode.y}`;
    }

    fitToContent(animate) {
        const stage = this.stage.node();
        if (!stage) return;
        const bounds = stage.getBBox();
        if (bounds.width <= 0 || bounds.height <= 0) return;
        const width = Math.max(this.element.clientWidth, 320);
        const height = Math.max(this.element.clientHeight, 320);
        const scale = Math.min(1.2, width / (bounds.width + 80), height / (bounds.height + 100));
        const transform = d3.zoomIdentity
            .translate(width / 2 - (bounds.x + bounds.width / 2) * scale, height / 2 - (bounds.y + bounds.height / 2) * scale)
            .scale(scale);
        const selection = animate ? this.svg.transition().duration(180) : this.svg;
        selection.call(this.zoom.transform, transform);
    }

    zoomBy(factor) {
        this.svg.call(this.zoom.scaleBy, Number.isFinite(factor) && factor > 0 ? factor : 1);
    }

    dispose() {
        this.svg.on(".zoom", null);
        this.svg.selectAll("*").remove();
    }
}

export function initializeWorkflowRunMap(element) {
    disposeWorkflowRunMap(element);
    workflowRunMaps.set(element, new WorkflowRunMap(element));
}

export function updateWorkflowRunMap(element, graph) {
    workflowRunMaps.get(element)?.update(graph);
}

export function zoomWorkflowRunMap(element, factor) {
    workflowRunMaps.get(element)?.zoomBy(factor);
}

export function resetWorkflowRunMap(element) {
    workflowRunMaps.get(element)?.fitToContent(true);
}

export function disposeWorkflowRunMap(element) {
    const map = workflowRunMaps.get(element);
    if (map) {
        map.dispose();
        workflowRunMaps.delete(element);
    }
}

class WorkflowRunMap {
    constructor(element) {
        this.element = element;
        this.svg = d3.select(element).select(".workflow-run-map-canvas");
        this.stage = this.svg.append("g").attr("class", "workflow-run-map-stage");
        this.laneGroup = this.stage.append("g").attr("class", "workflow-run-map-lanes");
        this.linkGroup = this.stage.append("g").attr("class", "workflow-run-map-links");
        this.labelGroup = this.stage.append("g").attr("class", "workflow-run-map-labels");
        this.nodeGroup = this.stage.append("g").attr("class", "workflow-run-map-nodes");
        this.markerId = `workflow-run-arrow-${++workflowRunMapSequence}`;

        this.svg.append("defs")
            .append("marker")
            .attr("id", this.markerId)
            .attr("viewBox", "0 -5 10 10")
            .attr("refX", 9)
            .attr("refY", 0)
            .attr("markerWidth", 7)
            .attr("markerHeight", 7)
            .attr("markerUnits", "userSpaceOnUse")
            .attr("orient", "auto")
            .append("path")
            .attr("d", "M0,-5L10,0L0,5")
            .attr("class", "workflow-run-map-arrow");

        this.zoom = d3.zoom()
            .scaleExtent([0.45, 3])
            .on("zoom", event => this.stage.attr("transform", event.transform));
        this.svg.call(this.zoom);
        this.structureKey = null;
    }

    update(graph) {
        const nodes = (graph?.nodes || []).map(node => ({ ...node, level: 0 }));
        const nodeById = new Map(nodes.map(node => [node.id, node]));
        const links = (graph?.links || [])
            .filter(link => nodeById.has(link.source) && nodeById.has(link.target))
            .map((link, index) => ({ ...link, id: `${link.source}->${link.target}:${index}` }));
        const incoming = new Map(nodes.map(node => [node.id, 0]));
        const outgoing = new Map(nodes.map(node => [node.id, []]));
        links.forEach(link => {
            incoming.set(link.target, (incoming.get(link.target) || 0) + 1);
            outgoing.get(link.source).push(link.target);
        });
        const pending = nodes.filter(node => incoming.get(node.id) === 0)
            .sort((left, right) => d3.ascending(left.sequence, right.sequence));
        const visited = new Set();
        while (pending.length) {
            const node = pending.shift();
            if (!visited.add(node.id)) continue;
            for (const targetId of outgoing.get(node.id)) {
                const target = nodeById.get(targetId);
                target.level = Math.max(target.level, node.level + 1);
                incoming.set(targetId, incoming.get(targetId) - 1);
                if (incoming.get(targetId) === 0) pending.push(target);
            }
        }
        nodes.filter(node => !visited.has(node.id)).forEach((node, index) => {
            node.level = Math.max(node.level, index);
        });

        const applicationGroups = d3.groups(nodes, node => node.application)
            .sort(([, left], [, right]) => d3.ascending(d3.min(left, node => node.sequence), d3.min(right, node => node.sequence)));
        let laneTop = 18;
        const applications = applicationGroups.map(([application, members], index) => {
            const maximumLevelOccupancy = d3.max(
                d3.rollups(members, values => values.length, node => node.level),
                ([, count]) => count) || 1;
            const height = Math.max(136, 34 + maximumLevelOccupancy * 86 + (maximumLevelOccupancy - 1) * 22 + 16);
            const lane = { application, index, top: laneTop, height };
            laneTop += height + 18;
            return lane;
        });
        const laneByApplication = new Map(applications.map(lane => [lane.application, lane]));
        const width = Math.max(620, (d3.max(nodes, node => node.level) + 1) * 380 + 220);
        const height = Math.max(300, laneTop + 9);
        const occupied = new Map();
        nodes.sort((left, right) => d3.ascending(left.sequence, right.sequence)).forEach(node => {
            const lane = laneByApplication.get(node.application);
            const positionKey = `${node.application}:${node.level}`;
            const offset = occupied.get(positionKey) || 0;
            occupied.set(positionKey, offset + 1);
            node.x = 180 + node.level * 380;
            node.y = lane.top + 77 + offset * 108;
        });
        links.forEach(link => {
            link.sourceNode = nodeById.get(link.source);
            link.targetNode = nodeById.get(link.target);
        });
        const structureKey = JSON.stringify({
            nodes: nodes.map(node => node.id),
            links: links.map(link => `${link.source}->${link.target}`),
            applications: applications.map(lane => lane.application)
        });
        const structureChanged = structureKey !== this.structureKey;
        this.structureKey = structureKey;

        this.svg.attr("viewBox", `0 0 ${width} ${height}`);
        this.element.style.minHeight = `${Math.min(520, Math.max(320, height))}px`;
        this.renderLanes(applications, width);
        this.renderLinks(links);
        this.renderNodes(nodes);
        if (structureChanged) queueMicrotask(() => this.fitToContent(false));
    }

    renderLanes(lanes, width) {
        const selection = this.laneGroup.selectAll("g")
            .data(lanes, lane => lane.application)
            .join(enter => {
                const group = enter.append("g").attr("class", "workflow-run-map-lane");
                group.append("rect");
                group.append("text");
                return group;
            });
        selection.select("rect")
            .attr("x", 18)
            .attr("y", lane => lane.top)
            .attr("width", width - 36)
            .attr("height", lane => lane.height)
            .attr("rx", 12);
        selection.select("text")
            .attr("x", 34)
            .attr("y", lane => lane.top + 20)
            .text(lane => lane.application);
    }

    renderLinks(links) {
        this.linkGroup.selectAll("path")
            .data(links, link => link.id)
            .join("path")
            .attr("class", link => `workflow-run-map-link${link.succeeded ? "" : " faulted"}`)
            .attr("marker-end", `url(#${this.markerId})`)
            .attr("d", link => {
                const startX = link.sourceNode.x + 105;
                const endX = link.targetNode.x - 105;
                const middleX = (startX + endX) / 2;
                return `M${startX},${link.sourceNode.y} C${middleX},${link.sourceNode.y} ${middleX},${link.targetNode.y} ${endX},${link.targetNode.y}`;
            });
        this.labelGroup.selectAll("text")
            .data(links, link => link.id)
            .join("text")
            .attr("class", "workflow-run-map-link-label")
            .attr("x", link => (link.sourceNode.x + link.targetNode.x) / 2)
            .attr("y", link => (link.sourceNode.y + link.targetNode.y) / 2 - 8)
            .text(link => `${link.operation} ${trimText(link.message, 14)} · ${link.metric || formatMilliseconds(link.handoffDurationMs)}`);
    }

    renderNodes(nodes) {
        const selection = this.nodeGroup.selectAll("g")
            .data(nodes, node => node.id)
            .join(enter => {
                const group = enter.append("g").attr("class", "workflow-run-map-node");
                group.append("rect").attr("x", -105).attr("y", -43).attr("width", 210).attr("height", 86).attr("rx", 11);
                group.append("text").attr("class", "workflow-run-map-application").attr("x", -90).attr("y", -21);
                group.append("text").attr("class", "workflow-run-map-structure").attr("x", 90).attr("y", -21);
                group.append("text").attr("class", "workflow-run-map-step").attr("x", -90).attr("y", 2);
                group.append("text").attr("class", "workflow-run-map-contract").attr("x", -90).attr("y", 23);
                group.append("text").attr("class", "workflow-run-map-duration").attr("x", 90).attr("y", 23);
                return group;
            });
        selection
            .attr("class", node => `workflow-run-map-node ${node.status}`)
            .attr("transform", node => `translate(${node.x},${node.y})`);
        selection.select(".workflow-run-map-application").text(node => trimText(node.application, 25));
        selection.select(".workflow-run-map-structure").text(node => {
            const labels = [];
            if (node.rootFanOutCount > 1) labels.push(`ROOT ×${node.rootFanOutCount}`);
            if (node.branchCount > 1) labels.push(`FORK ×${node.branchCount}`);
            if (node.mergeCount > 1) labels.push(`MERGE ×${node.mergeCount}`);
            if (node.terminal) labels.push("TERMINAL");
            return labels.join(" · ");
        });
        selection.select(".workflow-run-map-step").text(node => trimText(node.label, 28));
        selection.select(".workflow-run-map-contract").text(node => `Consumes ${trimText(node.contract, 16)}`);
        selection.select(".workflow-run-map-duration").text(node => node.metric || formatMilliseconds(node.durationMs));
    }

    fitToContent(animate) {
        const stage = this.stage.node();
        if (!stage) return;
        const bounds = stage.getBBox();
        if (bounds.width <= 0 || bounds.height <= 0) return;
        const width = Math.max(this.element.clientWidth, 320);
        const height = Math.max(this.element.clientHeight, 300);
        const scale = Math.min(1.15, width / (bounds.width + 70), height / (bounds.height + 70));
        const transform = d3.zoomIdentity
            .translate(width / 2 - (bounds.x + bounds.width / 2) * scale, height / 2 - (bounds.y + bounds.height / 2) * scale)
            .scale(scale);
        const selection = animate ? this.svg.transition().duration(180) : this.svg;
        selection.call(this.zoom.transform, transform);
    }

    zoomBy(factor) {
        const boundedFactor = Number.isFinite(factor) && factor > 0 ? factor : 1;
        this.svg.call(this.zoom.scaleBy, boundedFactor);
    }

    dispose() {
        this.svg.on(".zoom", null);
        this.svg.selectAll("*").remove();
    }
}

function edgePath(edge) {
    if (getNodeId(edge.source) === getNodeId(edge.target)) {
        return `M${edge.source.x + 62},${edge.source.y - 28} C${edge.source.x + 150},${edge.source.y - 130} ${edge.source.x - 150},${edge.source.y - 130} ${edge.source.x - 62},${edge.source.y - 28}`;
    }
    const dx = edge.target.x - edge.source.x;
    const dy = edge.target.y - edge.source.y;
    const distance = Math.max(1, Math.hypot(dx, dy));
    const startX = edge.source.x + dx / distance * 98;
    const startY = edge.source.y + dy / distance * 98;
    const endX = edge.target.x - dx / distance * 108;
    const endY = edge.target.y - dy / distance * 108;
    const bend = getNodeId(edge.source) < getNodeId(edge.target) ? 0.12 : -0.12;
    return `M${startX},${startY} Q${(startX + endX) / 2 + dy * bend},${(startY + endY) / 2 - dx * bend} ${endX},${endY}`;
}

function replicaEdgePath(edge) {
    if (getNodeId(edge.source) === getNodeId(edge.target)) {
        return `M${edge.source.x + 50},${edge.source.y - 22} C${edge.source.x + 125},${edge.source.y - 105} ${edge.source.x - 125},${edge.source.y - 105} ${edge.source.x - 50},${edge.source.y - 22}`;
    }
    const dx = edge.target.x - edge.source.x;
    const dy = edge.target.y - edge.source.y;
    const distance = Math.max(1, Math.hypot(dx, dy));
    const startX = edge.source.x + dx / distance * 87;
    const startY = edge.source.y + dy / distance * 47;
    const endX = edge.target.x - dx / distance * 96;
    const endY = edge.target.y - dy / distance * 52;
    const bend = getNodeId(edge.source) < getNodeId(edge.target) ? 0.1 : -0.1;
    return `M${startX},${startY} Q${(startX + endX) / 2 + dy * bend},${(startY + endY) / 2 - dx * bend} ${endX},${endY}`;
}

function edgeMidpoint(edge) {
    if (getNodeId(edge.source) === getNodeId(edge.target)) return { x: edge.source.x, y: edge.source.y - 92 };
    return { x: (edge.source.x + edge.target.x) / 2, y: (edge.source.y + edge.target.y) / 2 };
}

function getNodeId(value) {
    return typeof value === "string" ? value : value.id;
}

function trimText(value, maximumLength) {
    const text = String(value || "");
    return text.length > maximumLength ? `${text.slice(0, maximumLength - 1)}…` : text;
}

function formatFlowRate(value) {
    if (value >= 1) return d3.format(".1f")(value);
    if (value >= 0.01) return d3.format(".2f")(value);
    return d3.format(".3f")(value);
}

function formatMilliseconds(value) {
    const milliseconds = Number(value || 0);
    return milliseconds >= 1000 ? `${d3.format(".2~f")(milliseconds / 1000)} s` : `${d3.format(".3~f")(milliseconds)} ms`;
}
