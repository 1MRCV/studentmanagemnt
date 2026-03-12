properties([
  parameters([

    choice(
      name: 'ENVIRONMENT',
      choices: ['DEV', 'PRODUCTION'],
      description: 'Target deployment environment'
    ),

    choice(
      name: 'ACTION',
      choices: ['DEPLOY', 'ROLLBACK'],
      description: 'Select action — ROLLBACK redeploys the selected build'
    ),

    string(
      name: 'BRANCH',
      defaultValue: 'main',
      description: 'Git branch to build — only used when ENVIRONMENT=DEV, ACTION=DEPLOY'
    ),

    // ── Artifact dropdown ──────────────────────────────────────────────
    // Reads successful DEV builds from Jenkins API.
    // Works regardless of where agents are — runs on Jenkins controller.
    // Shows: Build_42 | 2026-03-12 06:22 | main
    //
    // WHY Jenkins API and NOT C:\jenkins-artifacts\DEV:
    //   Active Choices script runs on the Ubuntu Jenkins controller.
    //   The controller cannot read C:\ paths on the Windows agent.
    //   Jenkins API is always accessible from the controller.
    // ──────────────────────────────────────────────────────────────────
    [$class: 'CascadeChoiceParameter',
      name: 'ARTIFACT_BUILD',
      description: 'Select build to deploy (shows all successful DEV builds)',
      choiceType: 'PT_SINGLE_SELECT',
      filterable: true,
      script: [
        $class: 'GroovyScript',
        script: [
          $class: 'SecureGroovyScript',
          sandbox: false,
          script: '''
            import jenkins.model.Jenkins

            def job = Jenkins.instance.getItemByFullName("Jenkins-only")

            if (job == null) {
              return ["ERROR: Job 'Jenkins-only' not found in Jenkins"]
            }

            def list = []

            job.getBuilds().each { build ->
              // Only include successful DEV DEPLOY builds
              // (these are the builds that actually created artifacts)
              if (build.getResult()?.toString() == "SUCCESS") {
                def params = build.getAction(hudson.model.ParametersAction)
                def envParam = params?.getParameter("ENVIRONMENT")?.getValue()
                def actParam = params?.getParameter("ACTION")?.getValue()

                if (envParam == "DEV" && actParam == "DEPLOY") {
                  def date   = new java.text.SimpleDateFormat("yyyy-MM-dd HH:mm").format(build.getTime())
                  def branch = build.getEnvironment()?.get("GIT_BRANCH") ?: "unknown"
                  branch     = branch.replaceAll("^origin/", "")
                  list << "Build_${build.getNumber()} | ${date} | ${branch}"
                }
              }
            }

            return list.isEmpty()
              ? ["No DEV builds yet — run ENVIRONMENT=DEV, ACTION=DEPLOY first"]
              : list
          '''
        ]
      ]
    ]

  ])
])

pipeline {

  agent { label 'windows-agent' }

  environment {
    GIT_REPO           = 'https://github.com/1MRCV/studentmanagemnt.git'

    // Artifact folders on Windows agent
    DEV_ARTIFACT_ROOT  = 'C:\\jenkins-artifacts\\DEV'
    PROD_ARTIFACT_ROOT = 'C:\\jenkins-artifacts\\PROD'

    // IIS deploy paths
    DEV_PATH           = 'C:\\inetpub\\dev'
    PROD_PATH          = 'C:\\inetpub\\prod'

    // IIS site names
    DEV_SITE           = 'student-dev'
    PROD_SITE          = 'student-prod'

    // IIS app pool names
    DEV_POOL           = 'student-dev-pool'
    PROD_POOL          = 'student-prod-pool'

    // Ports
    DEV_PORT           = '8081'
    PROD_PORT          = '8082'
  }

  stages {

    // ── Label the build in Jenkins UI ─────────────────────────────────
    stage('Set Build Name') {
      steps {
        script {
          def date = new Date().format('yyyy-MM-dd')
          currentBuild.displayName = "#${env.BUILD_NUMBER} | ${params.ENVIRONMENT} | ${params.ACTION} | ${date}"
        }
      }
    }

    // ── Only for DEV DEPLOY ────────────────────────────────────────────
    stage('Clean Workspace') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION      == 'DEPLOY' }
        }
      }
      steps {
        deleteDir()
      }
    }

    stage('Checkout Code') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION      == 'DEPLOY' }
        }
      }
      steps {
        // gitTool: 'Git-Windows' — configure in:
        // Manage Jenkins > Global Tool Configuration > Git
        // Name: Git-Windows | Path: C:\Program Files\Git\cmd\git.exe
        checkout([
          $class: 'GitSCM',
          branches: [[name: "${params.BRANCH}"]],
          userRemoteConfigs: [[url: "${env.GIT_REPO}"]],
          gitTool: 'Git-Windows'
        ])
      }
    }

    stage('Build & Create Artifact') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION      == 'DEPLOY' }
        }
      }
      steps {
        powershell 'dotnet restore'
        powershell 'dotnet build --configuration Release'
        powershell 'dotnet publish StudentPortal.Web/StudentPortal.Web.csproj -c Release -o publish'

        powershell '''
          $build    = $env:BUILD_NUMBER
          $date     = Get-Date -Format "yyyy-MM-dd"
          $dateTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

          # Store artifact in DEV subfolder
          $root = "C:\\jenkins-artifacts\\DEV"

          if (!(Test-Path $root)) {
            New-Item -ItemType Directory -Path $root -Force | Out-Null
          }

          $folder = "$root\\build_${build}_$date"
          New-Item -ItemType Directory -Path $folder -Force | Out-Null

          Compress-Archive -Path "publish\\*" -DestinationPath "$folder\\artifact.zip" -Force

          $commit = git rev-parse --short HEAD
          $branch = git rev-parse --abbrev-ref HEAD

          @"
Application : StudentPortal
Build       : $build
Branch      : $branch
Commit      : $commit
Date        : $dateTime
"@ | Out-File "$folder\\build-info.txt" -Encoding UTF8

          Write-Host "Artifact created at $folder"
        '''
      }
    }

    // ── For PRODUCTION: copy artifact from DEV folder into PROD folder ─
    // PROD never builds — it only promotes a tested DEV artifact
    stage('Promote Artifact to PROD') {
      when {
        expression { params.ENVIRONMENT == 'PRODUCTION' }
      }
      steps {
        powershell """
          \$selected  = "${params.ARTIFACT_BUILD}"
          \$buildNum  = \$selected.Split("|")[0].Replace("Build_","").Trim()

          \$devRoot   = "${env.DEV_ARTIFACT_ROOT}"
          \$prodRoot  = "${env.PROD_ARTIFACT_ROOT}"

          # Find the artifact in DEV
          \$devFolder = Get-ChildItem \$devRoot -Directory -ErrorAction SilentlyContinue |
                        Where-Object { \$_.Name -like "build_\${buildNum}_*" } |
                        Select-Object -First 1

          if (\$null -eq \$devFolder) {
            Write-Error "Build_\$buildNum not found in DEV artifacts (\$devRoot)."
            exit 1
          }

          # Create PROD root if missing
          if (!(Test-Path \$prodRoot)) {
            New-Item -ItemType Directory -Path \$prodRoot -Force | Out-Null
          }

          \$prodFolder = Join-Path \$prodRoot \$devFolder.Name

          if (Test-Path \$prodFolder) {
            Write-Host "Artifact already in PROD: \$(\$devFolder.Name)"
          } else {
            Copy-Item -Path \$devFolder.FullName -Destination \$prodRoot -Recurse -Force
            Write-Host "Promoted \$(\$devFolder.Name) from DEV to PROD artifacts."
          }
        """
      }
    }

    // ── Deploy to IIS ─────────────────────────────────────────────────
    stage('Deploy to IIS') {
      steps {
        powershell """
          Import-Module WebAdministration

          \$selected = "${params.ARTIFACT_BUILD}"
          \$envName  = "${params.ENVIRONMENT}"

          # Always deploy from the environment's own artifact folder
          \$root = if (\$envName -eq "DEV") { "${env.DEV_ARTIFACT_ROOT}" } else { "${env.PROD_ARTIFACT_ROOT}" }

          # Resolve artifact folder from selection
          if ([string]::IsNullOrWhiteSpace(\$selected) -or \$selected -like "No DEV*" -or \$selected -like "ERROR:*") {
            Write-Host "No artifact selected — using latest in \$root."
            \$folder = Get-ChildItem \$root -Directory |
                       Sort-Object LastWriteTime -Descending |
                       Select-Object -First 1
          } else {
            \$buildNum = \$selected.Split("|")[0].Replace("Build_","").Trim()
            \$folder   = Get-ChildItem \$root -Directory |
                         Where-Object { \$_.Name -like "build_\${buildNum}_*" } |
                         Select-Object -First 1
          }

          if (\$null -eq \$folder) {
            Write-Error "Artifact not found in \$root. Run a DEV build first."
            exit 1
          }

          \$zip = Join-Path \$folder.FullName "artifact.zip"

          if (!(Test-Path \$zip)) {
            Write-Error "artifact.zip missing in \$(\$folder.FullName)"
            exit 1
          }

          Write-Host "Deploying: \$(\$folder.Name) to \$envName"

          # IIS settings per environment
          if (\$envName -eq "DEV") {
            \$site = "${env.DEV_SITE}"
            \$pool = "${env.DEV_POOL}"
            \$path = "${env.DEV_PATH}"
            \$port = "${env.DEV_PORT}"
          } else {
            \$site = "${env.PROD_SITE}"
            \$pool = "${env.PROD_POOL}"
            \$path = "${env.PROD_PATH}"
            \$port = "${env.PROD_PORT}"
          }

          # Create deploy folder if missing
          if (!(Test-Path \$path)) {
            New-Item -ItemType Directory -Path \$path -Force | Out-Null
          }

          # Create App Pool if missing
          if (!(Test-Path "IIS:\\AppPools\\\$pool")) {
            New-WebAppPool -Name \$pool
            Set-ItemProperty "IIS:\\AppPools\\\$pool" managedRuntimeVersion ""
            Write-Host "Created App Pool: \$pool"
          }

          # Create IIS Site if missing
          if (!(Test-Path "IIS:\\Sites\\\$site")) {
            New-Website -Name \$site -Port \$port -PhysicalPath \$path -ApplicationPool \$pool
            Write-Host "Created IIS site: \$site on port \$port"
          }

          # Stop -> clean -> extract -> start
          Stop-WebAppPool -Name \$pool -ErrorAction SilentlyContinue
          Start-Sleep -Seconds 2

          Remove-Item "\$path\\*" -Recurse -Force -ErrorAction SilentlyContinue
          Expand-Archive -Path \$zip -DestinationPath \$path -Force

          Start-WebAppPool -Name \$pool
          Start-Website    -Name \$site

          Write-Host "SUCCESS: \$(\$folder.Name) deployed to \$site (port \$port)"
        """
      }
    }

    // ── Verify app pool is running ────────────────────────────────────
    stage('Verify') {
      steps {
        powershell """
          Import-Module WebAdministration

          \$pool = if ("${params.ENVIRONMENT}" -eq "DEV") { "${env.DEV_POOL}" } else { "${env.PROD_POOL}" }

          \$state = (Get-WebAppPoolState -Name \$pool).Value
          Write-Host "App Pool '\$pool' state: \$state"

          if (\$state -ne "Started") {
            Write-Error "App pool not running — deployment failed."
            exit 1
          }

          Write-Host "Verification passed."
        """
      }
    }

  }

  post {
    success {
      echo "Build #${BUILD_NUMBER} — ${params.ENVIRONMENT} ${params.ACTION} completed successfully."
    }
    failure {
      echo "Build #${BUILD_NUMBER} — ${params.ENVIRONMENT} ${params.ACTION} FAILED. Check logs."
    }
  }

}
