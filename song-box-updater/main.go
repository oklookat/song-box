package main

import (
	"context"
	"fmt"
	"os"
	"os/signal"
	"path"
	"path/filepath"
	"strings"
	"syscall"
	"time"

	"golang.org/x/sys/windows"
)

const (
	_WAIT_SEC         = 8
	_DETACHED_PROCESS = 0x00000008
	_WELCOME          = "==== oklookat/song-box-updater v1.0.0 ===="
	_PREFIX           = "> "
)

var (
	_exePath = ""
)

func main() {
	println(_WELCOME)
	exePath, err := os.Executable()
	chk(err, "os.Executable")
	_exePath = exePath
	exeDir := filepath.Dir(_exePath)

	log("Бу! Испугались? Не бойтесь!")
	log("Это просто обновление song-box.")
	log("Потом song-box попросит запуск от имени администратора.")
	log(fmt.Sprintf("Жду %d секунд, не закрывайте.", _WAIT_SEC))

	dots := ""
	for range _WAIT_SEC {
		dots += "."
		print(dots)
		time.Sleep(time.Second)
	}
	println("")

	log("Обновление...")

	chk(replaceNewFiles(), "replaceNewFiles")

	targetBinary := path.Join(exeDir, "song-box.exe")

	log("Запуск обновленного song-box...")

	chk(runAsAdmin(targetBinary), "runAsAdmin")
}

// Заменяет все *.new файлы в текущей директории
func replaceNewFiles() error {
	entries, err := os.ReadDir(".")
	if err != nil {
		return err
	}

	exeBase := filepath.Base(_exePath)

	for _, entry := range entries {
		if entry.IsDir() {
			continue
		}
		name := entry.Name()
		if strings.HasSuffix(name, ".new") {
			origName := strings.TrimSuffix(name, ".new")
			// Skip current binary.
			if filepath.Clean(origName) == exeBase {
				continue
			}
			if err := os.Remove(origName); err != nil && !os.IsNotExist(err) {
				return fmt.Errorf("removing %s: %w", origName, err)
			}
			// Переименовать .new -> оригинал
			if err := os.Rename(name, origName); err != nil {
				return fmt.Errorf("renaming %s -> %s: %w", name, origName, err)
			}
			fmt.Printf("Updated: %s\n", origName)
		}
	}
	return nil
}

// Run independent binary (Windows-only)
func launchBinary(binaryPath string) error {
	log("Launching: " + binaryPath)
	cmdLine := `"` + binaryPath + `"` // обязательно в кавычках на случай пробелов в пути
	argv, err := syscall.UTF16PtrFromString(cmdLine)
	if err != nil {
		return fmt.Errorf("func UTF16PtrFromString: %w", err)
	}

	return syscall.CreateProcess(
		nil,   // appName = nil
		argv,  // commandLine
		nil,   // process attributes
		nil,   // thread attributes
		false, // inherit handles
		syscall.CREATE_NEW_PROCESS_GROUP|_DETACHED_PROCESS,
		nil,                    // environment
		nil,                    // current directory
		&syscall.StartupInfo{}, // startup info
		&syscall.ProcessInformation{},
	)
}

func log(msg string) {
	println(_PREFIX + msg)
}

func chk(err error, executor string) {
	if err == nil {
		return
	}
	log("======== ОШИБКА (ЭТО ПЛОХО) ========")
	log(executor + " error: " + err.Error())
	waitForExit()
	os.Exit(1)
}

func waitForExit() {
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	fmt.Println("Для выхода закройте это окно, или нажмите в нем CTRL+C.")
	<-ctx.Done()
}

func runAsAdmin(appPath string) error {
	verb, err := syscall.UTF16PtrFromString("runas")
	if err != nil {
		return err
	}
	exe, err := syscall.UTF16PtrFromString(appPath)
	if err != nil {
		return err
	}

	// ShellExecute returns only error
	return windows.ShellExecute(0, verb, exe, nil, nil, windows.SW_NORMAL)
}
