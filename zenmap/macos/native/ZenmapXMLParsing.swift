import Foundation

extension ContentView {
    func parseNmapXML(at url: URL) -> [ScannedHost] {
        guard FileManager.default.fileExists(atPath: url.path),
              let data = try? Data(contentsOf: url),
              !data.isEmpty else {
            return []
        }

        if let hosts = parseCompleteNmapXML(data: data) {
            return hosts
        }

        guard let text = String(data: data, encoding: .utf8) else {
            return []
        }

        return parsePartialNmapXML(text: text)
    }

    func applyLiveHosts(_ parsedHosts: [ScannedHost]) {
        let previousAddress = hosts.first(where: { $0.id == selectedHostID })?.address
        guard hostsFingerprint(parsedHosts) != hostsFingerprint(hosts) else {
            return
        }

        hosts = parsedHosts
        if let previousAddress,
           let matched = parsedHosts.first(where: { $0.address == previousAddress }) {
            selectedHostID = matched.id
        } else {
            selectedHostID = parsedHosts.first?.id
        }
    }

    func refreshLiveHostsFromXMLIfNeeded() {
        guard isRunning, !lastXMLPath.isEmpty else {
            return
        }

        let xmlHosts = parseNmapXML(at: URL(fileURLWithPath: lastXMLPath))
        let liveHosts = parseLiveOutputHosts(from: output)
        applyLiveHosts(mergeScanHosts(xmlHosts, liveHosts))
    }

    func hostsFingerprint(_ hosts: [ScannedHost]) -> [(String, String, String, [(String, String, String, String, String, String)])] {
        hosts.map { host in
            (
                host.address,
                host.hostname,
                host.status,
                host.ports.map { port in
                    (
                        port.protocolName,
                        port.portNumber,
                        port.state,
                        port.serviceName,
                        port.product,
                        port.version
                    )
                }
            )
        }
    }

    func parseCompleteNmapXML(data: Data) -> [ScannedHost]? {
        let parser = XMLParser(data: data)
        let delegate = NmapXMLParserDelegate()
        parser.delegate = delegate

        guard parser.parse() else {
            return nil
        }

        return mergeScanHosts(delegate.hosts, delegate.hostHints)
    }

    func parsePartialNmapXML(text: String) -> [ScannedHost] {
        let finished = parseFragmentHosts(in: text, pattern: #"<host\b[^>]*>[\s\S]*?</host>"#)
        let hints = parseFragmentHosts(in: text, pattern: #"<hosthint\b[^>]*>[\s\S]*?</hosthint>"#)
        return mergeScanHosts(finished, hints)
    }

    func parseFragmentHosts(in text: String, pattern: String) -> [ScannedHost] {
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else {
            return []
        }

        let range = NSRange(text.startIndex..<text.endIndex, in: text)
        var hosts: [ScannedHost] = []

        regex.enumerateMatches(in: text, options: [], range: range) { match, _, _ in
            guard let match,
                  let hostRange = Range(match.range, in: text),
                  let hostData = String(text[hostRange]).data(using: .utf8),
                  let parsed = parseCompleteNmapXML(data: hostData) else {
                return
            }
            hosts.append(contentsOf: parsed)
        }

        return hosts
    }

    func parseLiveOutputHosts(from outputText: String) -> [ScannedHost] {
        var hostsByAddress: [String: ScannedHost] = [:]

        let reportRegex = try? NSRegularExpression(
            pattern: #"Nmap scan report for (?:(\S+) \((\d[^)]*)\)|(\S+))"#,
            options: [.caseInsensitive]
        )
        let reportRange = NSRange(outputText.startIndex..<outputText.endIndex, in: outputText)
        reportRegex?.enumerateMatches(in: outputText, options: [], range: reportRange) { match, _, _ in
            guard let match else { return }
            let hostname = match.range(at: 1).location != NSNotFound ? String(outputText[Range(match.range(at: 1), in: outputText)!]) : ""
            let parentheticalIP = match.range(at: 2).location != NSNotFound ? String(outputText[Range(match.range(at: 2), in: outputText)!]) : ""
            let bareTarget = match.range(at: 3).location != NSNotFound ? String(outputText[Range(match.range(at: 3), in: outputText)!]) : ""
            let address = parentheticalIP.isEmpty ? bareTarget : parentheticalIP
            guard !address.isEmpty else { return }
            if var existing = hostsByAddress[address] {
                if existing.hostname.isEmpty && !hostname.isEmpty {
                    existing.hostname = hostname
                    hostsByAddress[address] = existing
                }
            } else {
                hostsByAddress[address] = ScannedHost(address: address, hostname: hostname, status: "up", ports: [])
            }
        }

        let discoveredRegex = try? NSRegularExpression(
            pattern: #"Discovered open port (\d+)/(tcp|udp|sctp) on (\S+)"#,
            options: [.caseInsensitive]
        )
        discoveredRegex?.enumerateMatches(in: outputText, options: [], range: reportRange) { match, _, _ in
            guard let match,
                  let portRange = Range(match.range(at: 1), in: outputText),
                  let protoRange = Range(match.range(at: 2), in: outputText),
                  let addressRange = Range(match.range(at: 3), in: outputText) else {
                return
            }
            let portNumber = String(outputText[portRange])
            let protocolName = String(outputText[protoRange]).lowercased()
            let address = String(outputText[addressRange])
            var host = hostsByAddress[address] ?? ScannedHost(address: address, hostname: "", status: "up", ports: [])
            let exists = host.ports.contains { $0.protocolName.lowercased() == protocolName && $0.portNumber == portNumber }
            if !exists {
                host.ports.append(
                    ScannedPort(
                        hostAddress: address,
                        protocolName: protocolName,
                        portNumber: portNumber,
                        state: "open",
                        serviceName: "",
                        product: "",
                        version: "",
                        extraInfo: ""
                    )
                )
            }
            hostsByAddress[address] = host
        }

        return Array(hostsByAddress.values)
    }

    func mergeScanHosts(_ primary: [ScannedHost], _ secondary: [ScannedHost]) -> [ScannedHost] {
        var merged: [String: ScannedHost] = [:]
        var order: [String] = []

        for source in [primary, secondary] {
            for host in source {
                let key = host.address.isEmpty ? host.hostname : host.address
                guard !key.isEmpty else { continue }
                if var existing = merged[key] {
                    if existing.hostname.isEmpty && !host.hostname.isEmpty {
                        existing.hostname = host.hostname
                    }
                    if (existing.status.isEmpty || existing.status == "unknown") && !host.status.isEmpty {
                        existing.status = host.status
                    }
                    existing.ports = mergePorts(existing.ports, host.ports)
                    merged[key] = existing
                } else {
                    merged[key] = host
                    order.append(key)
                }
            }
        }

        return order.compactMap { merged[$0] }
    }

    func mergePorts(_ primary: [ScannedPort], _ secondary: [ScannedPort]) -> [ScannedPort] {
        var merged: [String: ScannedPort] = [:]
        var order: [String] = []

        for source in [primary, secondary] {
            for port in source {
                let key = "\(port.protocolName.lowercased())/\(port.portNumber)"
                if var existing = merged[key] {
                    if existing.serviceName.isEmpty { existing.serviceName = port.serviceName }
                    if existing.product.isEmpty { existing.product = port.product }
                    if existing.version.isEmpty { existing.version = port.version }
                    if existing.extraInfo.isEmpty { existing.extraInfo = port.extraInfo }
                    if existing.state.isEmpty || existing.state == "unknown" { existing.state = port.state }
                    merged[key] = existing
                } else {
                    merged[key] = port
                    order.append(key)
                }
            }
        }

        return order.compactMap { merged[$0] }
    }

    final class NmapXMLParserDelegate: NSObject, XMLParserDelegate {
        private(set) var hosts: [ScannedHost] = []
        private(set) var hostHints: [ScannedHost] = []

        private var currentHost: ScannedHost?
        private var currentPort: ScannedPort?
        private var isInsideHostnames = false
        private var isHostHint = false

        func parser(
            _ parser: XMLParser,
            didStartElement elementName: String,
            namespaceURI: String?,
            qualifiedName qName: String?,
            attributes attributeDict: [String: String] = [:]
        ) {
            switch elementName {
            case "host":
                isHostHint = false
                currentHost = ScannedHost(address: "", hostname: "", status: "unknown", ports: [])

            case "hosthint":
                isHostHint = true
                currentHost = ScannedHost(address: "", hostname: "", status: "unknown", ports: [])

            case "status":
                currentHost?.status = attributeDict["state"] ?? "unknown"

            case "address":
                let addrType = attributeDict["addrtype"] ?? ""
                let candidate = attributeDict["addr"] ?? ""
                guard !candidate.isEmpty else { return }
                if currentHost?.address.isEmpty == true || addrType == "ipv4" || addrType == "ipv6" {
                    currentHost?.address = candidate
                }

            case "hostnames":
                isInsideHostnames = true

            case "hostname":
                if isInsideHostnames, currentHost?.hostname.isEmpty == true {
                    currentHost?.hostname = attributeDict["name"] ?? ""
                }

            case "port":
                currentPort = ScannedPort(
                    hostAddress: currentHost?.address ?? "",
                    protocolName: attributeDict["protocol"] ?? "",
                    portNumber: attributeDict["portid"] ?? "",
                    state: "unknown",
                    serviceName: "",
                    product: "",
                    version: "",
                    extraInfo: ""
                )

            case "state":
                currentPort?.state = attributeDict["state"] ?? "unknown"

            case "service":
                currentPort?.serviceName = attributeDict["name"] ?? ""
                currentPort?.product = attributeDict["product"] ?? ""
                currentPort?.version = attributeDict["version"] ?? ""
                currentPort?.extraInfo = attributeDict["extrainfo"] ?? ""

            default:
                break
            }
        }

        func parser(
            _ parser: XMLParser,
            didEndElement elementName: String,
            namespaceURI: String?,
            qualifiedName qName: String?
        ) {
            switch elementName {
            case "hostnames":
                isInsideHostnames = false

            case "port":
                if let currentPort {
                    currentHost?.ports.append(currentPort)
                }
                currentPort = nil

            case "host", "hosthint":
                if let currentHost {
                    if isHostHint {
                        hostHints.append(currentHost)
                    } else {
                        hosts.append(currentHost)
                    }
                }
                currentHost = nil
                isHostHint = false

            default:
                break
            }
        }
    }
}
