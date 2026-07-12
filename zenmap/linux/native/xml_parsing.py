"""Parse Nmap XML output into platform-neutral host models."""

from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from pathlib import Path

from .models import ScannedHost, ScannedPort

# ``<host\b`` does not match ``<hosthint>``.
_COMPLETE_HOST_RE = re.compile(r"<host\b[^>]*>.*?</host>", re.DOTALL | re.IGNORECASE)
_COMPLETE_HOSTHINT_RE = re.compile(
    r"<hosthint\b[^>]*>.*?</hosthint>",
    re.DOTALL | re.IGNORECASE,
)
_DISCOVERED_OPEN_PORT_RE = re.compile(
    r"Discovered open port (\d+)/(tcp|udp|sctp) on (\S+)",
    re.IGNORECASE,
)
_SCAN_REPORT_RE = re.compile(
    r"Nmap scan report for (?:(\S+) \((\d[^)]*)\)|(\S+))",
    re.IGNORECASE,
)


def parse_nmap_xml(path: str | Path) -> list[ScannedHost]:
    """Parse Nmap XML, including incomplete mid-scan files.

    Mid-scan XML often contains finished ``<hosthint>`` discovery records before
    any closed ``<host>`` element. Full-document parses fail until the scan ends,
    so fall back to extracting complete host/hosthint fragments.
    """
    xml_path = Path(path)
    if not xml_path.is_file():
        return []

    try:
        text = xml_path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return []

    if not text.strip():
        return []

    try:
        root = ET.fromstring(text)
    except ET.ParseError:
        return _merge_hosts(
            _parse_complete_elements(text, _COMPLETE_HOST_RE),
            _parse_complete_elements(text, _COMPLETE_HOSTHINT_RE),
        )

    finished = [
        host
        for host_element in root.findall("host")
        if (host := _parse_host(host_element)) is not None
    ]
    hints = [
        host
        for hint_element in root.findall("hosthint")
        if (host := _parse_host(hint_element)) is not None
    ]
    return _merge_hosts(finished, hints)


def parse_live_output_hosts(output_text: str) -> list[ScannedHost]:
    """Build provisional hosts/ports from verbose nmap stdout."""
    hosts_by_address: dict[str, ScannedHost] = {}

    for match in _SCAN_REPORT_RE.finditer(output_text):
        hostname, parenthetical_ip, bare_target = match.groups()
        if parenthetical_ip:
            address = parenthetical_ip
            host_name = hostname or ""
        else:
            address = bare_target or ""
            host_name = ""
        if not address:
            continue
        host = hosts_by_address.get(address)
        if host is None:
            hosts_by_address[address] = ScannedHost(
                address=address,
                hostname=host_name,
                status="up",
                ports=[],
            )
        elif host_name and not host.hostname:
            host.hostname = host_name

    for match in _DISCOVERED_OPEN_PORT_RE.finditer(output_text):
        port_number, protocol_name, address = match.groups()
        host = hosts_by_address.get(address)
        if host is None:
            host = ScannedHost(address=address, hostname="", status="up", ports=[])
            hosts_by_address[address] = host
        key = (protocol_name.lower(), port_number)
        if any(
            (port.protocol_name.lower(), port.port_number) == key for port in host.ports
        ):
            continue
        host.ports.append(
            ScannedPort(
                host_address=address,
                protocol_name=protocol_name.lower(),
                port_number=port_number,
                state="open",
            )
        )

    return list(hosts_by_address.values())


def merge_scan_hosts(
    xml_hosts: list[ScannedHost],
    live_output_hosts: list[ScannedHost],
) -> list[ScannedHost]:
    """Prefer richer XML host records, then fill gaps from live stdout."""
    return _merge_hosts(xml_hosts, live_output_hosts)


def hosts_fingerprint(hosts: list[ScannedHost]) -> tuple:
    """Stable fingerprint used to skip redundant live UI refreshes."""
    return tuple(
        (
            host.address,
            host.hostname,
            host.status,
            tuple(
                (
                    port.protocol_name,
                    port.port_number,
                    port.state,
                    port.service_name,
                    port.product,
                    port.version,
                )
                for port in host.ports
            ),
        )
        for host in hosts
    )


def _parse_complete_elements(text: str, pattern: re.Pattern[str]) -> list[ScannedHost]:
    hosts: list[ScannedHost] = []
    for match in pattern.finditer(text):
        try:
            host_element = ET.fromstring(match.group(0))
        except ET.ParseError:
            continue
        host = _parse_host(host_element)
        if host is not None:
            hosts.append(host)
    return hosts


def _merge_hosts(
    primary: list[ScannedHost],
    secondary: list[ScannedHost],
) -> list[ScannedHost]:
    merged: dict[str, ScannedHost] = {}
    order: list[str] = []

    for source in (primary, secondary):
        for host in source:
            key = host.address or host.hostname
            if not key:
                continue
            existing = merged.get(key)
            if existing is None:
                merged[key] = ScannedHost(
                    address=host.address,
                    hostname=host.hostname,
                    status=host.status,
                    ports=list(host.ports),
                )
                order.append(key)
                continue
            if host.hostname and not existing.hostname:
                existing.hostname = host.hostname
            if host.status and existing.status in {"", "unknown"}:
                existing.status = host.status
            existing.ports = _merge_ports(existing.ports, host.ports)

    return [merged[key] for key in order]


def _merge_ports(
    primary: list[ScannedPort],
    secondary: list[ScannedPort],
) -> list[ScannedPort]:
    merged: dict[tuple[str, str], ScannedPort] = {}
    order: list[tuple[str, str]] = []

    for source in (primary, secondary):
        for port in source:
            key = (port.protocol_name.lower(), port.port_number)
            existing = merged.get(key)
            if existing is None:
                merged[key] = ScannedPort(
                    host_address=port.host_address,
                    protocol_name=port.protocol_name,
                    port_number=port.port_number,
                    state=port.state,
                    service_name=port.service_name,
                    product=port.product,
                    version=port.version,
                    extra_info=port.extra_info,
                )
                order.append(key)
                continue
            if port.service_name and not existing.service_name:
                existing.service_name = port.service_name
            if port.product and not existing.product:
                existing.product = port.product
            if port.version and not existing.version:
                existing.version = port.version
            if port.extra_info and not existing.extra_info:
                existing.extra_info = port.extra_info
            if port.state and existing.state in {"", "unknown"}:
                existing.state = port.state

    return [merged[key] for key in order]


def _parse_host(host_element: ET.Element) -> ScannedHost | None:
    status_element = host_element.find("status")
    status = status_element.get("state", "unknown") if status_element is not None else "unknown"

    address = ""
    for address_element in host_element.findall("address"):
        addr_type = address_element.get("addrtype", "")
        candidate = address_element.get("addr", "")
        if not candidate:
            continue
        if not address or addr_type in {"ipv4", "ipv6"}:
            address = candidate
            if addr_type in {"ipv4", "ipv6"}:
                break

    hostname = ""
    hostnames_element = host_element.find("hostnames")
    if hostnames_element is not None:
        hostname_element = hostnames_element.find("hostname")
        if hostname_element is not None:
            hostname = hostname_element.get("name", "")

    ports: list[ScannedPort] = []
    ports_element = host_element.find("ports")
    if ports_element is not None:
        for port_element in ports_element.findall("port"):
            port = _parse_port(port_element, address)
            if port is not None:
                ports.append(port)

    if not address and not hostname:
        return None

    return ScannedHost(address=address, hostname=hostname, status=status, ports=ports)


def _parse_port(port_element: ET.Element, host_address: str) -> ScannedPort | None:
    state_element = port_element.find("state")
    service_element = port_element.find("service")

    return ScannedPort(
        host_address=host_address,
        protocol_name=port_element.get("protocol", ""),
        port_number=port_element.get("portid", ""),
        state=state_element.get("state", "unknown") if state_element is not None else "unknown",
        service_name=service_element.get("name", "") if service_element is not None else "",
        product=service_element.get("product", "") if service_element is not None else "",
        version=service_element.get("version", "") if service_element is not None else "",
        extra_info=service_element.get("extrainfo", "") if service_element is not None else "",
    )
