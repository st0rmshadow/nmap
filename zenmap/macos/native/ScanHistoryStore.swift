import Foundation
import Combine

final class ScanHistoryStore: ObservableObject {
    private static let savedScansDefaultsKey = "Zenmap.SavedScans"

    @Published var savedScans: [SavedScan] = [] {
        didSet {
            saveSavedScans()
        }
    }
    @Published var selectedSavedScanID: SavedScan.ID?
    @Published var selectedSavedScanIDs: Set<SavedScan.ID> = []

    init() {
        savedScans = Self.loadSavedScans()
        pruneOrphanSessionScans()
    }

    func clearSavedScans(deleteFiles: Bool = false) {
        if deleteFiles {
            for scan in savedScans {
                deleteSavedScanFile(at: scan.xmlPath)
            }
        }

        savedScans.removeAll()
        selectedSavedScanID = nil
        selectedSavedScanIDs.removeAll()
    }

    func removeSavedScan(id savedScanID: SavedScan.ID, deleteFile: Bool = false) {
        if deleteFile,
           let scan = savedScans.first(where: { $0.id == savedScanID }) {
            deleteSavedScanFile(at: scan.xmlPath)
        }

        savedScans.removeAll { $0.id == savedScanID }

        if selectedSavedScanID == savedScanID {
            selectedSavedScanID = nil
        }
    }

    func persistSavedScan(id savedScanID: SavedScan.ID) -> Bool {
        guard let index = savedScans.firstIndex(where: { $0.id == savedScanID }),
              savedScans[index].ephemeral else {
            return false
        }

        let scan = savedScans[index]
        guard FileManager.default.fileExists(atPath: scan.xmlPath),
              let destinationPath = copyXMLToSavedScansDirectory(
                  sourcePath: scan.xmlPath,
                  title: scan.title,
                  date: scan.scannedAt
              ) else {
            return false
        }

        deleteSavedScanFile(at: scan.xmlPath)
        savedScans[index] = SavedScan(
            id: scan.id,
            title: scan.title,
            command: scan.command,
            xmlPath: destinationPath,
            scannedAt: scan.scannedAt,
            hostCount: scan.hostCount,
            portCount: scan.portCount,
            notes: scan.notes,
            tags: scan.tags,
            ephemeral: false
        )
        return true
    }

    func cleanupEphemeralScans() {
        let remaining = savedScans.filter { scan in
            if scan.ephemeral {
                deleteSavedScanFile(at: scan.xmlPath)
                return false
            }
            return true
        }

        savedScans = remaining
        if let selectedSavedScanID,
           !savedScans.contains(where: { $0.id == selectedSavedScanID }) {
            self.selectedSavedScanID = nil
        }
        selectedSavedScanIDs = selectedSavedScanIDs.filter { id in
            savedScans.contains(where: { $0.id == id })
        }
    }

    private func pruneOrphanSessionScans() {
        guard let sessionDirectory = sessionScansDirectoryURL(),
              let files = try? FileManager.default.contentsOfDirectory(
                  at: sessionDirectory,
                  includingPropertiesForKeys: nil
              ) else {
            return
        }

        let referencedPaths = Set(savedScans.map(\.xmlPath))
        for fileURL in files where fileURL.pathExtension.lowercased() == "xml" {
            let path = fileURL.path
            if !referencedPaths.contains(path) {
                try? FileManager.default.removeItem(at: fileURL)
            }
        }
    }

    private func sessionScansDirectoryURL() -> URL? {
        guard let applicationSupportURL = FileManager.default.urls(
            for: .applicationSupportDirectory,
            in: .userDomainMask
        ).first else {
            return nil
        }

        return applicationSupportURL
            .appendingPathComponent("Zenmap", isDirectory: true)
            .appendingPathComponent("SessionScans", isDirectory: true)
    }

    private func deleteSavedScanFile(at path: String) {
        guard FileManager.default.fileExists(atPath: path) else {
            return
        }

        try? FileManager.default.removeItem(atPath: path)
    }

    private static func loadSavedScans() -> [SavedScan] {
        guard let data = UserDefaults.standard.data(forKey: savedScansDefaultsKey),
              let decoded = try? JSONDecoder().decode([SavedScan].self, from: data) else {
            return []
        }

        return decoded.filter { FileManager.default.fileExists(atPath: $0.xmlPath) }
    }

    private func saveSavedScans() {
        let persistentScans = savedScans.filter { !$0.ephemeral }
        guard let data = try? JSONEncoder().encode(persistentScans) else {
            return
        }

        UserDefaults.standard.set(data, forKey: Self.savedScansDefaultsKey)
    }

    private func copyXMLToSavedScansDirectory(sourcePath: String, title: String, date: Date) -> String? {
        let sourceURL = URL(fileURLWithPath: sourcePath)

        guard FileManager.default.fileExists(atPath: sourceURL.path),
              let savedScansDirectory = savedScansDirectoryURL() else {
            return nil
        }

        do {
            try FileManager.default.createDirectory(
                at: savedScansDirectory,
                withIntermediateDirectories: true
            )

            let filename = savedScanFilename(title: title, date: date)
            let destinationURL = savedScansDirectory.appendingPathComponent(filename)

            if FileManager.default.fileExists(atPath: destinationURL.path) {
                try FileManager.default.removeItem(at: destinationURL)
            }

            try FileManager.default.copyItem(at: sourceURL, to: destinationURL)
            return destinationURL.path
        } catch {
            return nil
        }
    }

    private func savedScanFilename(title: String, date: Date) -> String {
        let timestamp = ISO8601DateFormatter()
            .string(from: date)
            .replacingOccurrences(of: ":", with: "-")
        let baseTitle = (title as NSString).deletingPathExtension
        let safeTitle = baseTitle
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "/", with: "-")
            .replacingOccurrences(of: ":", with: "-")
            .replacingOccurrences(of: " ", with: "_")
        let finalTitle = safeTitle.isEmpty ? "nmap-scan" : safeTitle

        return "\(timestamp)-\(finalTitle).xml"
    }

    private func savedScansDirectoryURL() -> URL? {
        guard let applicationSupportURL = FileManager.default.urls(
            for: .applicationSupportDirectory,
            in: .userDomainMask
        ).first else {
            return nil
        }

        return applicationSupportURL
            .appendingPathComponent("Zenmap", isDirectory: true)
            .appendingPathComponent("SavedScans", isDirectory: true)
    }
}
